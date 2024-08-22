using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildTimeHistory.Models;
using Humanizer;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using Task = System.Threading.Tasks.Task;

namespace BuildTimeHistory
{
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    // The following AuoLoad attributes are to make sure that all load scenarios are covered.
    // Even launching a .slnf file from the JumpList (which was particularly hard to track down)
    // I'm sure they're not all needed, but as I spent ages slowly adding more until everything worked
    // I don't want to waste more time removing any.
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionHasMultipleProjects, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionHasSingleProject, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.EmptySolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndNotBuildingAndNotDebugging_string, PackageAutoLoadFlags.BackgroundLoad)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(BuildTimeHistoryPackage.PackageGuidString)]
    public sealed class BuildTimeHistoryPackage : AsyncPackage, IVsSolutionEvents, IVsUpdateSolutionEvents2
    {
        public const string PackageGuidString = "c0e7666d-0fc1-4e88-9c61-0468227a9922";

        private IVsSolution2 solution;
        private IVsSolutionBuildManager2 sbm;
        private uint updateSolutionEventsCookie;
        private uint solutionEventsCookie;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            solution = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution2;
            solution?.AdviseSolutionEvents(this, out solutionEventsCookie);

            sbm = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;
            sbm?.AdviseUpdateSolutionEvents(this, out updateSolutionEventsCookie);

            await OutputPane.Instance.WriteAsync($"{Vsix.Name} v{Vsix.Version}");            
        }

        #region Unused Interface
        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate)
        {
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Cancel()
        {
            return VSConstants.S_OK;
        }

        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int UpdateProjectCfg_Begin(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int UpdateProjectCfg_Done(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel)
        {
            return VSConstants.S_OK;
        } 
        #endregion


        readonly Stopwatch _buildTimer = new Stopwatch();

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            SolutionOpen();
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Begin(ref int pfCancelUpdate)
        {
            _buildTimer.Restart();

            return VSConstants.S_OK;
        }

        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ProcessFinish(fSucceeded == 1, fCancelCommand == 1);

            return VSConstants.S_OK;
        }

        private string GetHistoryFilePath(string solutionName, int daysPast = 0)
        {
            var dateOfInstrest = DateTime.Now.AddDays(-daysPast);

            var day = dateOfInstrest.ToString("yyyy-MM-dd");

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BuildTimeTracker",
                solutionName,
                dateOfInstrest.ToString("yyyy-MM"),
                $"{day}.json");

            return path;
        }

        private async Task SaveTodaysRecordAsync(string solutionName, DailyBuildHistory record)
        {
            try
            {
                var path = GetHistoryFilePath(solutionName);

                var dir = Path.GetDirectoryName(path);

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, JsonConvert.SerializeObject(record));
            }
            catch (Exception ex)
            {
                await OutputPane.Instance.WriteAsync("Failed to save history file for today");
                await OutputPane.Instance.WriteAsync(ex.Message);
                await OutputPane.Instance.WriteAsync(ex.Source);
                await OutputPane.Instance.WriteAsync(ex.StackTrace);
            }
        }

        private async Task<(DailyBuildHistory, int)> GetMostRecentDaysRecordAsync(string solutionName)
        {
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    var path = GetHistoryFilePath(solutionName, i);

                    if (File.Exists(path))
                    {
                        return (JsonConvert.DeserializeObject<DailyBuildHistory>(File.ReadAllText(path)), i);
                    }
                }
            }
            catch (Exception ex)
            {
                await OutputPane.Instance.WriteAsync("Failed to load history file for today");
                await OutputPane.Instance.WriteAsync(ex.Message);
                await OutputPane.Instance.WriteAsync(ex.Source);
                await OutputPane.Instance.WriteAsync(ex.StackTrace);
            }

            return (new DailyBuildHistory(), int.MinValue);
        }

        private async Task<DailyBuildHistory> GetTodaysRecordAsync(string solutionName)
        {
            try
            {
                var path = GetHistoryFilePath(solutionName);

                if (File.Exists(path))
                {
                    return JsonConvert.DeserializeObject<DailyBuildHistory>(File.ReadAllText(path));
                }
            }
            catch (Exception ex)
            {
                await OutputPane.Instance.WriteAsync("Failed to load history file for today");
                await OutputPane.Instance.WriteAsync(ex.Message);
                await OutputPane.Instance.WriteAsync(ex.Source);
                await OutputPane.Instance.WriteAsync(ex.StackTrace);
            }

            return new DailyBuildHistory();
        }

        private void ProcessFinish(bool wasSuccessful, bool wasCancelled)
        {
            // If a build was started before the extension package loaded the time won't have been started.
            bool includeTimeInHistory = _buildTimer.IsRunning;

            _buildTimer.Stop();

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    var buildDuration = _buildTimer.ElapsedMilliseconds;
                    var solutionName = GetCurrentSolutionName();

                    var todaysRecord = await GetTodaysRecordAsync(solutionName);

                    var sb = new StringBuilder();

                    sb.Append($"{DateTime.Now.ToShortTimeString()}>");

                    if (wasSuccessful)
                    {
                        sb.AppendLine($"Build completed successfully after {TimeSpan.FromMilliseconds(buildDuration).Humanize()}");
                        BuildHistoryItem item = new BuildHistoryItem { RecordDate = DateTime.Now, Status = Enums.BuildCompletionStatus.Succeeded };

                        if (includeTimeInHistory)
                        {
                            item.BuildTime = buildDuration;
                        }

                        todaysRecord.BuildHistory.Add(item);
                    }
                    else if (wasCancelled)
                    {
                        sb.AppendLine($"Build was cancelled after {TimeSpan.FromMilliseconds(buildDuration).Humanize()}");
                        BuildHistoryItem item = new BuildHistoryItem { RecordDate = DateTime.Now, Status = Enums.BuildCompletionStatus.Cancelled };

                        if (includeTimeInHistory)
                        {
                            item.BuildTime = buildDuration;
                        }

                        todaysRecord.BuildHistory.Add(item);
                    }
                    else
                    {
                        sb.AppendLine($"Build failed after {TimeSpan.FromMilliseconds(buildDuration).Humanize()}");
                        BuildHistoryItem item = new BuildHistoryItem { RecordDate = DateTime.Now, Status = Enums.BuildCompletionStatus.Failed };

                        if (includeTimeInHistory)
                        {
                            item.BuildTime = buildDuration;
                        }

                        todaysRecord.BuildHistory.Add(item);
                    }

                    if (!includeTimeInHistory)
                    {
                        await OutputPane.Instance.WriteAsync("** Build time is unavailable and won't be added to the cumulative history.");
                    }

                    await SaveTodaysRecordAsync(solutionName, todaysRecord);

                    var totalBuildTime = TimeSpan.FromMilliseconds(todaysRecord.TotalBuildTime);
                    var averageBuildTime = TimeSpan.FromMilliseconds(todaysRecord.AverageBuildTime);

                    sb.Append($"Today's build summary: ");
                    sb.Append($"{todaysRecord.TotalCount} build {(todaysRecord.TotalCount > 1 ? "s" : string.Empty)} ");
                    sb.Append($"({todaysRecord.TotalSuccess} successful, {todaysRecord.TotalFailed} failed, {todaysRecord.TotalCancelled} cancelled) ");
                    sb.Append($"taking a total of {totalBuildTime.Humanize()} ({totalBuildTime.Hours:00}:{totalBuildTime.Minutes:00}:{totalBuildTime.Seconds:00}) ");
                    sb.Append($"with an average of {averageBuildTime.Humanize()} ({averageBuildTime.Hours:00}:{averageBuildTime.Minutes:00}:{averageBuildTime.Seconds:00}) per build ");

                    await OutputPane.Instance.WriteAsync(sb.ToString());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    await OutputPane.Instance.WriteAsync(ex.Message);
                }
            });
        }

        private string GetCurrentSolutionName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (solution != null)
            {
                solution.GetSolutionInfo(out string solutionDirectory, out string solutionFile, out string userOptionsFile);

                if (!string.IsNullOrWhiteSpace(solutionFile))
                {
                    return System.IO.Path.GetFileNameWithoutExtension(solutionFile);
                }
            }

            return string.Empty;
        }

        private void SolutionOpen()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                try
                {
                    var solutionName = GetCurrentSolutionName();

                    var (latestRecord, daysAgo) = await GetMostRecentDaysRecordAsync(solutionName);

                    if (daysAgo > int.MinValue && latestRecord.TotalCount > 0)
                    {
                        var sb = new StringBuilder();

                        if (daysAgo == 0)
                        {
                            sb.Append("Today's build summary: ");
                        }
                        else if (daysAgo == 1)
                        {
                            sb.Append("Yesterday's build summary: ");
                        }
                        else
                        {
                            sb.Append($"Build summary for {DateTime.Now.AddDays(daysAgo):dddd, MMMM d} : ");
                        }

                        var totalBuildTime = TimeSpan.FromMilliseconds(latestRecord.TotalBuildTime);
                        var averageBuildTime = TimeSpan.FromMilliseconds(latestRecord.AverageBuildTime);

                        sb.Append($"{latestRecord.TotalCount} build{(latestRecord.TotalCount > 1 ? "s" : string.Empty)} ");
                        sb.Append($"({latestRecord.TotalSuccess} successful, {latestRecord.TotalFailed} failed, {latestRecord.TotalCancelled} cancelled) ");
                        sb.Append($"taking a total of {totalBuildTime.Humanize()} ({totalBuildTime.Hours:00}:{totalBuildTime.Minutes:00}:{totalBuildTime.Seconds:00})");
                        sb.Append($"with an average of {averageBuildTime.Humanize()} ({averageBuildTime.Hours:00}:{averageBuildTime.Minutes:00}:{averageBuildTime.Seconds:00}) per build");


                        await OutputPane.Instance.WriteAsync(sb.ToString());
                    }
                    else
                    {
                        await OutputPane.Instance.WriteAsync("No previous history available.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    await OutputPane.Instance.WriteAsync(ex.Message);
                }
            });

        }
    }
}
