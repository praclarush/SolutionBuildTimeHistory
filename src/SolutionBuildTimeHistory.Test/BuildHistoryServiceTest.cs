using BuildTimeHistory.Enums;
using BuildTimeHistory.Models;
using BuildTimeHistory.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.IO;

namespace SolutionBuildTimeHistory.Test
{
    [TestClass]
    public class BuildHistoryServiceTest
    {
        private const string _emptyFileName = "NoData";
        private const string _yesterdayFileName = "Yesterday";
        private const string _todayFileName = "Today";
        private const string _unknownLastDateFileName = "Unknown";

        private string _testPath = "D:\\Workspace\\Programming\\Test Data";

        private Random _random = new Random();

        [TestInitialize]
        public void Init()
        {
            Cleanup();
            GenerateTestFile(_todayFileName, 5);
            GenerateTestFile(_yesterdayFileName, 5, excludeToday: true);
            GenerateTestFile(_unknownLastDateFileName, randomDays: true);
        }

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
        [DataRow(_todayFileName)]
        [DataRow(_yesterdayFileName)]
        [DataRow(_emptyFileName)]
        [DataRow(_unknownLastDateFileName)]
        public void GetMostRecentDayRecord_ReturnsDailyBuildHistory(string solutionName)
        {            
            BuildHistoryService service = new BuildHistoryService(_testPath); 
            bool initResult = service.Initialize(solutionName);
            
            Assert.IsTrue(initResult, $"Unable to initialize service for solution: {solutionName}");

            DailyBuildHistory buildDay = service.GetMostRecentDayRecord();

            switch (solutionName)
            {
                case _todayFileName:                                       
                    Assert.IsTrue(buildDay.Date.Date == DateTime.Now.Date, "No Data for Today");
                    break;
                case _yesterdayFileName:
                    Assert.IsTrue(buildDay.Date.Date == DateTime.Now.Date.AddDays(-1), "No Data for Yesterday");
                    break;
                case _emptyFileName:                                       
                    Assert.IsTrue(buildDay == null, "Empty Solution is not Empty");
                    break;
                case _unknownLastDateFileName:
                    Assert.IsTrue(buildDay != null);
                    break;
                default:
                    Assert.Fail($"Solution Name Not MappedL: {solutionName}");
                    break;
            }
        }

        [TestMethod]
        [DataRow(_todayFileName)]
        [DataRow(_yesterdayFileName)]
        public void GetTodaysRecord_ReturnsDailyBuildHistory(string solutionName)
        {
            BuildHistoryService service = new BuildHistoryService(solutionName);
            bool initResult = service.Initialize(solutionName);

            Assert.IsTrue(initResult, $"Unable to initialize service for solution: {solutionName}");

            DailyBuildHistory buildDay = service.GetTodaysRecord();

            Assert.IsTrue(buildDay.Date.Date == DateTime.Now.Date);
        }

        [TestMethod]
        public void AddRecord_returnsBoolean()
        {
            BuildHistoryService service = new BuildHistoryService(_todayFileName);
            bool initResult = service.Initialize(_todayFileName);

            Assert.IsTrue(initResult, $"Unable to initialize service for solution: {_todayFileName}");

            var historyItem = new BuildHistoryItem()
            {
                BuildTime = RandomBuildTime(),
                RecordDate = DateTime.Now,
                Status = RandomStatus()
            };

            var result = service.AddRecord(historyItem);
            Assert.IsTrue(result, "Unable to Add History Item");
            Assert.IsTrue(service.GetTodaysRecord().BuildHistory.Contains(historyItem), "History Item was not found in Build History");
        }

        [TestMethod]
        public void Save_ReturnsBool()
        {
            var solutionName = "SaveTest";
            BuildHistoryService service = new BuildHistoryService(_testPath);
            bool initResult = service.Initialize(solutionName);

            Assert.IsTrue(initResult, $"Unable to initialize service for solution: {solutionName}");

            var currentDate = new DateTime(2024, 06, 01);

            service.BuildHistory.DateCreated = currentDate;
            service.BuildHistory.LastUpdated = currentDate;

            DailyBuildHistory dailyHistoryItem = new DailyBuildHistory()
            {
                Date = currentDate,
                BuildHistory = new System.Collections.Generic.List<BuildHistoryItem>()
            };

            dailyHistoryItem.BuildHistory.Add(new BuildHistoryItem()
            {
                BuildTime = 1000,
                RecordDate = currentDate,
                Status = BuildCompletionStatus.Succeeded
            });

            service.BuildHistory.DailyBuildHistory.Add(dailyHistoryItem);

            var result = service.Save();
            Assert.IsTrue(result);

            var expected = "{\"SolutionName\":\"SaveTest\",\"DaysToKeep\":0,\"LastUpdated\":\"2024-06-01T00:00:00\",\"DateCreated\":\"2024-06-01T00:00:00\",\"DailyBuildHistory\":[{\"Date\":\"2024-06-01T00:00:00\",\"BuildHistory\":[{\"RecordDate\":\"2024-06-01T00:00:00\",\"Status\":1,\"BuildTime\":1000.0}]}]}";

            var actual = File.ReadAllText(service.FilePath);

            Assert.AreEqual(expected, actual);
        }


        private void GenerateTestFile(string solutionName, int numDaysToPreload = 0, int numBuildsToSimulate = 0, bool excludeToday = false, bool randomDays = false)
        {
            var service = new BuildHistoryService(_testPath);
            var initResult = service.Initialize(solutionName);

            var solutionHistory = service.BuildHistory;

            bool todayExcluded = false;

            if (numDaysToPreload == 0)
            {
                numDaysToPreload = _random.Next(1, 31);
            }

            for (int i = 0; i < numDaysToPreload; i++)
            {
                if (excludeToday && !todayExcluded) 
                { 
                    i++;
                    numDaysToPreload++;
                    todayExcluded = true;
                }

                DateTime currentDay;

                if (!randomDays)
                {
                    var daysPast = i * -1;
                    currentDay = DateTime.Now.AddDays(daysPast);
                }
                else
                {
                    var randomMonth = _random.Next(1, 12);
                    var randomDay = _random.Next(1, 28);
                    currentDay = new DateTime(DateTime.Now.Year, randomMonth, randomDay);
                }

                if (numBuildsToSimulate == 0)
                {
                    numBuildsToSimulate = _random.Next(1, 20);
                }

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
