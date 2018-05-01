//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SqlTools.ServiceLayer.QueryExecution.Contracts.ExecuteRequests;
using Microsoft.SqlTools.ServiceLayer.Test.Common;
using Xunit;

namespace Microsoft.SqlTools.ServiceLayer.PerfTests
{
    public class QueryExecutionTests
    {
        [Fact]
        public async Task QueryResultSummaryOnPremTest()
        {
            TestServerType serverType = TestServerType.OnPrem;

            using (SelfCleaningTempFile queryTempFile = new SelfCleaningTempFile())
            using (TestServiceDriverProvider testService = new TestServiceDriverProvider())
            {
                const string query = Scripts.MasterBasicQuery;

                await testService.ConnectForQuery(serverType, query, queryTempFile.FilePath, SqlTestDb.MasterDatabaseName);
                var queryResult = await testService.CalculateRunTime(() => testService.RunQueryAndWaitToComplete(queryTempFile.FilePath, query), true);

                Assert.NotNull(queryResult);
                Assert.True(queryResult.BatchSummaries.Any(x => x.ResultSetSummaries.Any(r => r.RowCount > 0)));

                await testService.Disconnect(queryTempFile.FilePath);
            }
        }

        [Fact]
        public async Task QueryResultFirstOnPremTest()
        {
            TestServerType serverType = TestServerType.OnPrem;

            using (SelfCleaningTempFile queryTempFile = new SelfCleaningTempFile())
            using (TestServiceDriverProvider testService = new TestServiceDriverProvider())
            {
                const string query = Scripts.MasterBasicQuery;

                await testService.ConnectForQuery(serverType, query, queryTempFile.FilePath, SqlTestDb.MasterDatabaseName);

                var queryResult = await testService.CalculateRunTime(async () =>
                {
                    await testService.RunQueryAndWaitToComplete(queryTempFile.FilePath, query);
                    return await testService.ExecuteSubset(queryTempFile.FilePath, 0, 0, 0, 100);
                }, true);

                Assert.NotNull(queryResult);
                Assert.NotNull(queryResult.ResultSubset);
                Assert.True(queryResult.ResultSubset.Rows.Any());

                await testService.Disconnect(queryTempFile.FilePath);
            }
        }

        [Fact]
        public async Task MediumQueryResultCompleteOnPremTest()
        {
            TestServerType serverType = TestServerType.OnPrem;

            using (SelfCleaningTempFile queryTempFile = new SelfCleaningTempFile())
            using (TestServiceDriverProvider testService = new TestServiceDriverProvider())
            {
                const string query = @"
                SELECT * FROM sys.all_objects o
                join sys.all_objects o2 on o2.object_id = o.object_id
                join sys.all_columns c on o.object_id = c.object_id
                join sys.all_sql_modules b on b.object_id = o.object_id
                join sys.all_parameters p on p.object_id = o.object_id
                ";

                await testService.ConnectForQuery(serverType, query, queryTempFile.FilePath, SqlTestDb.MasterDatabaseName);

                var queryResult = await testService.CalculateRunTime(async () =>
                {
                    return await testService.RunQueryAndWaitToComplete(queryTempFile.FilePath, query, 10000);
                   
                }, false);

                Assert.NotNull(queryResult);
                Assert.True(queryResult.BatchSummaries.Any(x => x.ResultSetSummaries.Any(r => r.RowCount > 0)));
                testService.PrintTestResult(TimeSpan.Parse(queryResult.BatchSummaries[0].ExecutionElapsed).TotalMilliseconds);

                await testService.Disconnect(queryTempFile.FilePath);
            }
        }

        [Fact]
        public async Task MediumQueryFirstResultSetOnPremTest()
        {
            TestServerType serverType = TestServerType.OnPrem;

            using (SelfCleaningTempFile queryTempFile = new SelfCleaningTempFile())
            using (TestServiceDriverProvider testService = new TestServiceDriverProvider())
            {
                const string query = @"
                SELECT * FROM sys.all_objects o
                join sys.all_objects o2 on o2.object_id = o.object_id
                join sys.all_columns c on o.object_id = c.object_id
                join sys.all_sql_modules b on b.object_id = o.object_id
                join sys.all_parameters p on p.object_id = o.object_id
                ";

                await testService.ConnectForQuery(serverType, query, queryTempFile.FilePath, SqlTestDb.MasterDatabaseName);

                var queryResult = await testService.CalculateRunTime(async () =>
                {
                    return await testService.RunQueryAndWaitForFirstResultSet(queryTempFile.FilePath, query, 10000);

                }, true);

                Assert.NotNull(queryResult);
                Assert.True(queryResult.ResultSetSummary.RowCount > 0);

                await testService.Disconnect(queryTempFile.FilePath);
            }
        }

        [Fact]
        [CreateTestDb(TestServerType.OnPrem)]
        public async Task CancelQueryOnPremTest()
        {
            TestServerType serverType = TestServerType.OnPrem;

            using (SelfCleaningTempFile queryTempFile = new SelfCleaningTempFile())
            using (TestServiceDriverProvider testService = new TestServiceDriverProvider())
            {
                await testService.ConnectForQuery(serverType, Scripts.DelayQuery, queryTempFile.FilePath, Common.PerfTestDatabaseName);
                var queryParams = new ExecuteDocumentSelectionParams
                {
                    OwnerUri = queryTempFile.FilePath,
                    QuerySelection = null
                };

                var result = await testService.Driver.SendRequest(ExecuteDocumentSelectionRequest.Type, queryParams);
                if (result != null)
                {
                    TestTimer timer = new TestTimer() { PrintResult = true };
                    await testService.ExecuteWithTimeout(timer, 100000, async () => 
                    {
                        var cancelQueryResult = await testService.CancelQuery(queryTempFile.FilePath);
                        return true;
                    },  TimeSpan.FromMilliseconds(10));
                }
                else
                {
                    Assert.True(false, "Failed to run the query");
                }

                await testService.Disconnect(queryTempFile.FilePath);
            }
        }
    }
}
