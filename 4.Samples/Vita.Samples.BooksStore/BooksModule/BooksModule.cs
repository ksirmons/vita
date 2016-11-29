﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Vita.Entities;
using Vita.Modules.Calendar;
using Vita.Modules.Logging;
using Vita.Samples.BookStore.Api;

namespace Vita.Samples.BookStore {

  //We could register Book entities directly at program startup. But for better code modularity, we create 
  // a data module that handles all book-related functionality - including entity registration, stored procedures, etc.
  // We also define a small static extension class to add handy entity-creation methods.
  public partial class BooksModule : EntityModule {
    public static readonly Version CurrentVersion = new Version("1.4.0.0");
    // Event codes for scheduled events
    public const string EventCodeRestock = "Restock";
    public const string EventCodeAskBookReview = "AskBookReview";

    //Services
    ICalendarService _calendarService;

    public BooksModule(EntityArea area) : base(area, "Books", "Books module", version: CurrentVersion) {
      Requires<CalendarEntityModule>(); 

      RegisterEntities(typeof(IBook), typeof(IPublisher), typeof(IAuthor), typeof(IBookAuthor), typeof(IBookReview),
                       typeof(IUser), typeof(IBookOrder), typeof(IBookOrderLine),  typeof(ICoupon), typeof(IImage));
      
      //Register companion types that describe keys and indexes on entities
      RegisterCompanionTypes(
          typeof(IBookKeys), typeof(IAuthorKeys), typeof(IPublisherKeys));
      //Add extra tracking columns in book, order, coupon entities  
      var tranLogExt = new TransactionLogModelExtender();
      tranLogExt.AddUpdateStampColumns(new[] { typeof(IBook), typeof(IBookOrder), typeof(IBookOrderLine), typeof(ICoupon) },
        createIdPropertyName: "CreatedIn", updateIdPropertyName: "UpdatedIn");
      App.ModelExtenders.Add(tranLogExt);
      //Set cached types
      App.CacheSettings.AddCachedTypes(CacheType.FullSet, typeof(IBook), typeof(IPublisher), typeof(IAuthor), typeof(IBookAuthor));
      App.CacheSettings.AddCachedTypes(CacheType.Sparse, typeof(IBookOrder), typeof(IBookOrderLine), typeof(IUser), typeof(IImage));
      //Register api controllers
      App.ApiConfiguration.RegisterControllerTypes(typeof(CatalogController), typeof(UserAccountController), typeof(SignupController));

      RegisterViews();
    }//method

    private void RegisterViews() {
      // DB Views -------------------------------------------------------------------
      var bookSet = ViewHelper.EntitySet<IBook>();
      var bolSet = ViewHelper.EntitySet<IBookOrderLine>();

      // Book sales query with grouping
      // MS SQL restrictions (https://msdn.microsoft.com/en-us/library/ms191432.aspx#Restrictions): 
      //   If GROUP BY is present, the VIEW definition must contain COUNT_BIG(*) and must not contain HAVING
      //   (it also cannot contain COUNT, only COUNT_BIG). VITA Linq engine automatically uses Count_big for Count(*)
      //  Notice we have to list all output properties inside group clause, in temp grouping object - to be able 
      // to include it in output clause
      var bookSalesQuery = from bol in bolSet
                           group bol by new { Id = bol.Book.Id, Title = bol.Book.Title, Publisher = bol.Book.Publisher.Name } into g
                           select new {
                             //intentionally using different order of properties (compared to IBookSales entity), just to check that it does not matter
                             Id = g.Key.Id, Title = g.Key.Title,
                             Count = g.Sum(l => l.Quantity),
                             Publisher = g.Key.Publisher,
                             Total = g.Sum(l => l.Price * l.Quantity),
                             _lineCount = g.Count() // to satisfy MS SQL requriement to include Count_BIG
                           };
      RegisterView<IBookSales>(bookSalesQuery, DbViewOptions.Materialized);

      // Other version of bookSales, with subqueries without grouping (testing bug fix: view output columns must have aliases)
      var bookSalesQuery2 = from b in bookSet
                            select new {
                              Id = b.Id, Title = b.Title, Publisher = b.Publisher.Name,
                              Count = bolSet.Where(bol => bol.Book == b).Sum(bol => bol.Quantity),
                              Total = bolSet.Where(bol => bol.Book == b).Sum(bol => bol.Price * bol.Quantity)
                            };
      RegisterView<IBookSales2>(bookSalesQuery2);

      //Fiction books query
      var fictionBookQuery = bookSet.Where(b => b.Category == BookCategory.Fiction);
      RegisterView<IFictionBook>(fictionBookQuery);
      // AuthorUser view - test for bug fix, reading values from outer join into nullable value (UserType?)
      var authQuery = from a in ViewHelper.EntitySet<IAuthor>()
                      select new { FirstName = a.FirstName, LastName = a.LastName, UserName = a.User.UserName, UserType = (UserType?)a.User.Type };
      RegisterView<IAuthorUser>(authQuery); 
    }

    public override void Init() {
      base.Init();
      _calendarService = App.GetService<ICalendarService>();
      _calendarService.EventFired += CalendarService_EventFired;
    }

    // A simple facility to test how calendars work. We schedule restocking event using CRON spec in SampleDataGenerator, 
    // so it should be fired every 5 minutes. We pretend to do the operation and add notes to the event. 
    private void CalendarService_EventFired(object sender, CalendarEventArgs e) {
      if (e.OwnerId == null && e.Code == EventCodeRestock) 
        e.Log += "Restocking operation executed at " + App.TimeService.UtcNow.ToString("s"); 
    }

    // Static method computing FullName computed property for an Author
    public static string GetFullName(IAuthor author) {
      return author.FirstName + " " + author.LastName;
    }
    // Static method computing order summary
    public static string GetOrderSummary(IBookOrder order) {
      return order.CreatedOn.ToString("s") + " " + order.User.DisplayName + ", Total: " + order.Total.ToString("###.##");
    }
    public static string GetOrderDisplay(IBookOrder order) {
      return string.Format("{0}, {1} items.", order.User.DisplayName, order.Lines.Count);
    }

    // Static method validating Book entity
    public static void ValidateBook(IBook book) {
      var session = EntityHelper.GetSession(book); 
      session.ValidateEntity(book, book.Price >= 0.0m, "PriceNegative", "Price", book.Price, "Price may not be negative");
    }


  }//BooksModule 

}
