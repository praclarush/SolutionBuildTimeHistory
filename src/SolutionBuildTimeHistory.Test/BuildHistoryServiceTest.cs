using BuildTimeHistory.Enums;
using BuildTimeHistory.Models;
using BuildTimeHistory.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace SolutionBuildTimeHistory.Test
{
    [TestClass]
    public class BuildHistoryServiceTest
    {
        private string _emptyFileName = "NoData";
        private string _yesterdayFileName = "Yesterday";
        private string _todayFileName = "Today";
        private string _testPath = "D:\\Workspace\\Programming\\Test Data";

        private Random _random = new Random();

        //[TestInitialize]
        //public void Init()
        //{

        //}

        [TestCleanup]
        public void Cleanup()
        {
            Array.ForEach(Directory.GetFiles(_testPath), File.Delete);
        }


        [TestMethod]
        public void Initialize_ReturnsTrue()
        {
           
            var solutionName = "testSolution";
            var service = new BuildHistoryService(_testPath);

            var result = service.Initialize(solutionName);
            
            Assert.IsTrue(service.IsInitialized, "Service Not Initailized");
        }

        [TestMethod]
        [DataRow("Today")]
        [DataRow("Yesterday")]
        [DataRow("NoData")]
        public void GetMostRecentDayRecord_ReturnsDailyBuildHistory(string solutionName)
        {            
            BuildHistoryService service;
            bool initResult = false;
            DailyBuildHistory buildDay;

            switch (solutionName)
            {
                case "Today":
                    GenerateTestFile(solutionName, 5, 10, false);
                    service = new BuildHistoryService(_testPath);
                    initResult = service.Initialize(solutionName);
                    Assert.IsTrue(initResult, $"Unable to initialize service for solution: {solutionName}");

                    buildDay = service.GetMostRecentDayRecord();

                    Assert.IsTrue(buildDay.Date.Date == DateTime.Now.Date);
                    break;
                case "Yesterday":
                    GenerateTestFile(solutionName, 5, 10, true);
                    service = new BuildHistoryService(_testPath);
                    initResult = service.Initialize(solutionName);
                    Assert.IsTrue(initResult, $"Unable to initialize service for solution: {solutionName}");

                    buildDay = service.GetMostRecentDayRecord();

                    Assert.IsTrue(buildDay.Date.Date == DateTime.Now.Date.AddDays(-1));
                    break;
                case "NoData":                   
                    service = new BuildHistoryService(_testPath);
                    initResult = service.Initialize(solutionName);
                    Assert.IsTrue(initResult, $"Unable to initialize service for solution: {solutionName}");

                    buildDay = service.GetMostRecentDayRecord();

                    Assert.IsTrue(buildDay == null);
                    break;
                default:
                    Assert.Fail($"Solution Name Not MappedL: {solutionName}");
                    break;
            }

        }


        private void GenerateTestFile(string solutionName, int numDaysToPreload, int numBuildsToSimulate, bool excludeToday)
        {
            var service = new BuildHistoryService(_testPath);
            var initResult = service.Initialize(solutionName);

            var solutionHistory = service.BuildHistory;

            for (int i = 0; i < numDaysToPreload; i++)
            {
                if (excludeToday) { i++; }

                var daysPast = i * -1;
                var currentDay = DateTime.Now.AddDays(daysPast);

                var day = new BuildTimeHistory.Models.DailyBuildHistory()
                {
                    Date = currentDay
                };

                for (int j = 0; j < numBuildsToSimulate; j++)
                {
                    var build = new BuildHistoryItem()
                    {
                        RecordDate = currentDay,
                        Status = RandomStatus(),
                        BuildTime = RandomBuildTime()
                    };

                    day.BuildHistory.Add(build);
                }

                solutionHistory.DailyBuildHistory.Add(day);
            }

            service.Save();
        }

        private BuildCompletionStatus RandomStatus()
        {
            var value = _random.Next(1, 3);
            return (BuildCompletionStatus)value;
        }

        private double RandomBuildTime()
        {
            var value = _random.Next(2000, 600000);
            return Convert.ToDouble(value);
        }
    }
}
