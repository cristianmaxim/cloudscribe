﻿// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Author:					Joe Audette
// Created:					2015-11-16
// Last Modified:			2015-12-15
// 

using cloudscribe.Core.Models.Logging;
using Microsoft.Data.Entity;
using Microsoft.Data.Entity.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cloudscribe.Core.Repositories.EF
{
    public class LogRepository : ILogRepository
    {
        public LogRepository(
            IServiceProvider serviceProvider,
            DbContextOptions<LoggingDbContext> options
            )
        {

            dbContextOptions = options;
            this.serviceProvider = serviceProvider;
            
        }

        // since most of the time this repo will be invoked for adding to the log 
        // we don't need this dbcontext most of the time
        // we do need it for querying the log but we can just create it lazily if it is needed
        private LoggingDbContext dbc = null;
        private LoggingDbContext dbContext
        {
            get
            {
                if(dbc == null)
                {
                    dbc = new LoggingDbContext(serviceProvider, dbContextOptions);
                }
                return dbc;
            }
        }

        private DbContextOptions dbContextOptions;
        private IServiceProvider serviceProvider;
        
        public int AddLogItem(
            DateTime logDate,
            string ipAddress,
            string culture,
            string url,
            string shortUrl,
            string thread,
            string logLevel,
            string logger,
            string message)
        {
            LogItem logItem = new LogItem();
            logItem.Id = 0;
            logItem.LogDateUtc = logDate;
            logItem.IpAddress = ipAddress;
            logItem.Culture = culture;
            logItem.Url = url;
            logItem.ShortUrl = shortUrl;
            logItem.Thread = thread;
            logItem.LogLevel = logLevel;
            logItem.Logger = logger;
            logItem.Message = message;

            using (var context = new LoggingDbContext(serviceProvider, dbContextOptions))
            {
                context.Add(logItem);
                context.SaveChanges();
            }

            // learned by experience for this situation we need to create transient instance of the dbcontext
            // for logging because the dbContext we have passed in is scoped to the request
            // and it causes problems to save changes on the context multiple times during a request
            // since we may log mutliple log items in a given request we need to create the dbcontext as needed
            // we can still use the normal dbContext for querying
            //dbContext.Add(logItem);
            //dbContext.SaveChanges();
           
            return logItem.Id;
        }

        public async Task<int> GetCount()
        {
            return await dbContext.LogItems.CountAsync<LogItem>();
        }

        
        public async Task<List<ILogItem>> GetPageAscending(
            int pageNumber,
            int pageSize)
        {
            int offset = (pageSize * pageNumber) - pageSize;

            var query = dbContext.LogItems.OrderBy(x => x.LogDateUtc)
                .Skip(offset)
                .Take(pageSize)
                .Select(p => p)
                ;

            return await query.AsNoTracking().ToListAsync<ILogItem>();
            
        }

        public async Task<List<ILogItem>> GetPageDescending(
            int pageNumber,
            int pageSize)
        {
            int offset = (pageSize * pageNumber) - pageSize;

            var query = dbContext.LogItems.OrderByDescending(x => x.LogDateUtc)
                .Skip(offset)
                .Take(pageSize)
                .Select(p => p)
                ;
            
            return await query.AsNoTracking().ToListAsync<ILogItem>();

        }

        public async Task<bool> DeleteAll()
        {
            dbContext.LogItems.RemoveAll();
            int rowsAffected = await dbContext.SaveChangesAsync();
            return rowsAffected > 0;
        }

        public async Task<bool> Delete(int logItemId)
        {
            var result = false;
            var itemToRemove = await dbContext.LogItems.SingleOrDefaultAsync(x => x.Id.Equals(logItemId));
            if(itemToRemove != null)
            {
                dbContext.LogItems.Remove(itemToRemove);
                int rowsAffected = await dbContext.SaveChangesAsync();
                result = rowsAffected > 0;
            }

            return result;
        }

        public async Task<bool> DeleteOlderThan(DateTime cutoffDateUtc)
        {
            var query = from l in dbContext.LogItems
                       where l.LogDateUtc < cutoffDateUtc
                        select l;

            dbContext.LogItems.RemoveRange(query);
            int rowsAffected = await dbContext.SaveChangesAsync();
            return rowsAffected > 0;
            
        }

        public async Task<bool> DeleteByLevel(string logLevel)
        {
            var query = from l in dbContext.LogItems
                        where l.LogLevel == logLevel
                        select l;

            dbContext.LogItems.RemoveRange(query);
            int rowsAffected = await dbContext.SaveChangesAsync();
            return rowsAffected > 0;
        }


    }
}