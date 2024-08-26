using BuildTimeHistory.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BuildTimeHistory.Services
{
    public class BuildHistoryService
    {
        private SolutionBuildHistory _buildHistory;
        private string _fileBasePath;
        private bool _isInitialized;
        private string _solutionName;

        public SolutionBuildHistory BuildHistory { get { return _buildHistory; } }
        
        public bool IsInitialized { get { return _isInitialized; } }

        public string FilePath { get { return Path.Combine(_fileBasePath, $"{_solutionName}.dat"); } }

        public string SolutionName { get { return _solutionName; } }

        public BuildHistoryService()
        {
            _fileBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BuildTimeTracker");
        }

        public BuildHistoryService(string fileBasePath)
        {
            _fileBasePath = fileBasePath;
        }

        public bool Initialize(string solutionName)
        {
            _solutionName = solutionName;

            try
            {
                if (File.Exists(FilePath))
                {
                    _buildHistory = JsonConvert.DeserializeObject<SolutionBuildHistory>(File.ReadAllText(FilePath));
                }
                else
                {
                    _buildHistory = new SolutionBuildHistory() { SolutionName = _solutionName, DateCreated = DateTime.Now, LastUpdated = DateTime.Now };
                }
            }
            catch (Exception)
            {
                _isInitialized = false;
                return false;
            }

            _isInitialized = true;
            return true;
        }

        public DailyBuildHistory GetMostRecentDayRecord()
        {
            ThrowIfNotInitalized();

            if (_buildHistory.DailyBuildHistory.Count == 0)
            {
                return null;
            }
            else if (_buildHistory.DailyBuildHistory.Count == 1)
            {
                return _buildHistory.DailyBuildHistory.First();
            }
            else
            {
                var item = _buildHistory.DailyBuildHistory.OrderByDescending(d => d.Date).FirstOrDefault();
                return item;
            }
        }


        public DailyBuildHistory GetTodaysRecord()
        {
            ThrowIfNotInitalized();

            var record = _buildHistory.DailyBuildHistory.SingleOrDefault(x => x.Date.Date == DateTime.Today.Date);

            if (record == null)
            {
                record = new DailyBuildHistory()
                {
                    Date = DateTime.Now
                };

                _buildHistory.DailyBuildHistory.Add(record);
            }

            return record;
        }

        public bool AddRecord(BuildHistoryItem record)
        {            
            _buildHistory.LastUpdated = DateTime.Now;
            
            var dailyRecord = GetTodaysRecord();
            dailyRecord.BuildHistory.Add(record);
            return true;
        }

        public bool Save()
        {
            ThrowIfNotInitalized();

            try
            {
                if (!Directory.Exists(_fileBasePath))
                {
                    Directory.CreateDirectory(_fileBasePath);
                }

                File.WriteAllText(FilePath, JsonConvert.SerializeObject(_buildHistory));

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Refresh()
        {
            ThrowIfNotInitalized();
            Initialize(_solutionName);
            return true;
        }

        private void ThrowIfNotInitalized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Solution History has not been initalized"); 
            }
        }
    }
}
