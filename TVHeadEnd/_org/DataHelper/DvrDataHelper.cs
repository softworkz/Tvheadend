namespace TVHeadEnd.DataHelper
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Controller.LiveTv;
    using MediaBrowser.Model.LiveTv;
    using MediaBrowser.Model.Logging;

    using TVHeadEnd.HTSP;

    public class DvrDataHelper
    {
        private readonly ILogger logger;
        private readonly Dictionary<string, HtsMessage> data;

        private readonly DateTime initialDateTimeUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DvrDataHelper(ILogger logger)
        {
            this.logger = logger;
            this.data = new Dictionary<string, HtsMessage>();
        }

        public void Clean()
        {
            lock (this.data)
            {
                this.data.Clear();
            }
        }

        public void DvrEntryAdd(HtsMessage message)
        {
            string id = message.GetString("id");
            lock (this.data)
            {
                if (this.data.ContainsKey(id))
                {
                    this.logger.Info("[TVHclient] DvrDataHelper.dvrEntryAdd id already in database - skip!" + message);
                    return;
                }

                this.data.Add(id, message);
            }
        }

        public void DvrEntryUpdate(HtsMessage message)
        {
            string id = message.GetString("id");
            lock (this.data)
            {
                HtsMessage oldMessage = this.data[id];
                if (oldMessage == null)
                {
                    this.logger.Info("[TVHclient] DvrDataHelper.dvrEntryUpdate id not in database - skip!" + message);
                    return;
                }

                foreach (KeyValuePair<string, object> entry in message)
                {
                    if (oldMessage.ContainsField(entry.Key))
                    {
                        oldMessage.RemoveField(entry.Key);
                    }

                    oldMessage.PutField(entry.Key, entry.Value);
                }
            }
        }

        public void DvrEntryDelete(HtsMessage message)
        {
            string id = message.GetString("id");
            lock (this.data)
            {
                this.data.Remove(id);
            }
        }

        public Task<IEnumerable<MyRecordingInfo>> BuildDvrInfos(CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew<IEnumerable<MyRecordingInfo>>(
                () =>
                    {
                        lock (this.data)
                        {
                            List<MyRecordingInfo> result = new List<MyRecordingInfo>();
                            foreach (KeyValuePair<string, HtsMessage> entry in this.data)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    this.logger.Info("[TVHclient] DvrDataHelper.buildDvrInfos, call canceled - returning part list.");
                                    return result;
                                }

                                HtsMessage m = entry.Value;
                                MyRecordingInfo ri = new MyRecordingInfo();

                                try
                                {
                                    if (m.ContainsField("error"))
                                    {
                                        // When TVHeadend recordings are removed, their info can
                                        // still be kept around with a status of "completed".
                                        // The only way to identify them is from the error string
                                        // which is set to "File missing". Use that to not show
                                        // non-existing deleted recordings.
                                        if (m.GetString("error").Contains("missing"))
                                        {
                                            continue;
                                        }
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("id"))
                                    {
                                        ri.Id = string.Empty + m.GetInt("id");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("path"))
                                    {
                                        ri.Path = string.Empty + m.GetString("path");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("url"))
                                    {
                                        ri.Url = string.Empty + m.GetString("url");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("channel"))
                                    {
                                        ri.ChannelId = string.Empty + m.GetInt("channel");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("start"))
                                    {
                                        long unixUtc = m.GetLong("start");
                                        ri.StartDate = this.initialDateTimeUtc.AddSeconds(unixUtc).ToUniversalTime();
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("stop"))
                                    {
                                        long unixUtc = m.GetLong("stop");
                                        ri.EndDate = this.initialDateTimeUtc.AddSeconds(unixUtc).ToUniversalTime();
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("title"))
                                    {
                                        ri.Name = m.GetString("title");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("description"))
                                    {
                                        ri.Overview = m.GetString("description");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("summary"))
                                    {
                                        ri.EpisodeTitle = m.GetString("summary");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                ri.HasImage = false;

                                // public string ImagePath { get; set; }
                                // public string ImageUrl { get; set; }
                                try
                                {
                                    if (m.ContainsField("state"))
                                    {
                                        string state = m.GetString("state");
                                        switch (state)
                                        {
                                            case "completed":
                                                ri.Status = RecordingStatus.Completed;
                                                break;
                                            case "scheduled":
                                                ri.Status = RecordingStatus.New;
                                                continue;

                                            // break;
                                            case "missed":
                                                ri.Status = RecordingStatus.Error;
                                                break;
                                            case "recording":
                                                ri.Status = RecordingStatus.InProgress;
                                                break;

                                            default:
                                                this.logger.Fatal("[TVHclient] DvrDataHelper.buildDvrInfos: state '" + state + "' not handled!");
                                                continue;

                                            // break;
                                        }
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                // Path must not be set to force emby use of the LiveTvService methods!!!!
                                // if (m.containsField("path"))
                                // {
                                // ri.Path = m.getString("path");
                                // }
                                try
                                {
                                    if (m.ContainsField("autorecId"))
                                    {
                                        ri.SeriesTimerId = m.GetString("autorecId");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("eventId"))
                                    {
                                        ri.ProgramId = string.Empty + m.GetInt("eventId");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                /*
                                        public ProgramAudio? Audio { get; set; }
                                        public ChannelType ChannelType { get; set; }
                                        public float? CommunityRating { get; set; }
                                        public List<string> Genres { get; set; }
                                        public bool? IsHD { get; set; }
                                        public bool IsKids { get; set; }
                                        public bool IsLive { get; set; }
                                        public bool IsMovie { get; set; }
                                        public bool IsNews { get; set; }
                                        public bool IsPremiere { get; set; }
                                        public bool IsRepeat { get; set; }
                                        public bool IsSeries { get; set; }
                                        public bool IsSports { get; set; }
                                        public string OfficialRating { get; set; }
                                        public DateTime? OriginalAirDate { get; set; }
                                        public string Url { get; set; }
                                 */
                                result.Add(ri);
                            }

                            return result;
                        }
                    });
        }

        public Task<IEnumerable<TimerInfo>> BuildPendingTimersInfos(CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew<IEnumerable<TimerInfo>>(
                () =>
                    {
                        lock (this.data)
                        {
                            List<TimerInfo> result = new List<TimerInfo>();
                            foreach (KeyValuePair<string, HtsMessage> entry in this.data)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    this.logger.Info("[TVHclient] DvrDataHelper.buildDvrInfos, call canceled - returning part list.");
                                    return result;
                                }

                                HtsMessage m = entry.Value;
                                TimerInfo ti = new TimerInfo();

                                try
                                {
                                    if (m.ContainsField("id"))
                                    {
                                        ti.Id = string.Empty + m.GetInt("id");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("channel"))
                                    {
                                        ti.ChannelId = string.Empty + m.GetInt("channel");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("start"))
                                    {
                                        long unixUtc = m.GetLong("start");
                                        ti.StartDate = this.initialDateTimeUtc.AddSeconds(unixUtc).ToUniversalTime();
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("stop"))
                                    {
                                        long unixUtc = m.GetLong("stop");
                                        ti.EndDate = this.initialDateTimeUtc.AddSeconds(unixUtc).ToUniversalTime();
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("title"))
                                    {
                                        ti.Name = m.GetString("title");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("description"))
                                    {
                                        ti.Overview = m.GetString("description");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("state"))
                                    {
                                        string state = m.GetString("state");
                                        switch (state)
                                        {
                                            case "scheduled":
                                                ti.Status = RecordingStatus.New;
                                                break;
                                            default:
                                                // only scheduled timers need to be delivered
                                                continue;
                                        }
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("startExtra"))
                                    {
                                        ti.PrePaddingSeconds = (int)m.GetLong("startExtra") * 60;
                                        ti.IsPrePaddingRequired = true;
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("stopExtra"))
                                    {
                                        ti.PostPaddingSeconds = (int)m.GetLong("stopExtra") * 60;
                                        ti.IsPostPaddingRequired = true;
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("priority"))
                                    {
                                        ti.Priority = m.GetInt("priority");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("autorecId"))
                                    {
                                        ti.SeriesTimerId = m.GetString("autorecId");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("eventId"))
                                    {
                                        ti.ProgramId = string.Empty + m.GetInt("eventId");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                result.Add(ti);
                            }

                            return result;
                        }
                    });
        }
    }
}