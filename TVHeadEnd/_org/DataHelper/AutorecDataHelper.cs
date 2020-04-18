namespace TVHeadEnd.DataHelper
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Controller.LiveTv;
    using MediaBrowser.Model.Logging;

    using TVHeadEnd.HTSP;

    public class AutorecDataHelper
    {
        private readonly ILogger logger;
        private readonly Dictionary<string, HtsMessage> data;

        public AutorecDataHelper(ILogger logger)
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

        public void AutorecEntryAdd(HtsMessage message)
        {
            string id = message.GetString("id");
            lock (this.data)
            {
                if (this.data.ContainsKey(id))
                {
                    this.logger.Info("[TVHclient] AutorecDataHelper.autorecEntryAdd id already in database - skip!" + message);
                    return;
                }

                this.data.Add(id, message);
            }
        }

        public void AutorecEntryUpdate(HtsMessage message)
        {
            string id = message.GetString("id");
            lock (this.data)
            {
                HtsMessage oldMessage = this.data[id];
                if (oldMessage == null)
                {
                    this.logger.Info("[TVHclient] AutorecDataHelper.autorecEntryAdd id not in database - skip!" + message);
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

        public void AutorecEntryDelete(HtsMessage message)
        {
            string id = message.GetString("id");
            lock (this.data)
            {
                this.data.Remove(id);
            }
        }

        public Task<IEnumerable<SeriesTimerInfo>> BuildAutorecInfos(CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew<IEnumerable<SeriesTimerInfo>>(
                () =>
                    {
                        lock (this.data)
                        {
                            List<SeriesTimerInfo> result = new List<SeriesTimerInfo>();

                            foreach (KeyValuePair<string, HtsMessage> entry in this.data)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    this.logger.Info("[TVHclient] DvrDataHelper.buildDvrInfos, call canceled - returning part list.");
                                    return result;
                                }

                                HtsMessage m = entry.Value;
                                SeriesTimerInfo sti = new SeriesTimerInfo();

                                try
                                {
                                    if (m.ContainsField("id"))
                                    {
                                        sti.Id = m.GetString("id");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("daysOfWeek"))
                                    {
                                        int daysOfWeek = m.GetInt("daysOfWeek");
                                        sti.Days = this.GetDayOfWeekListFromInt(daysOfWeek);
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                sti.StartDate = DateTimeOffset.Now.ToUniversalTime();

                                try
                                {
                                    if (m.ContainsField("retention"))
                                    {
                                        int retentionInDays = m.GetInt("retention");

                                        if (DateTimeOffset.MaxValue.AddDays(-retentionInDays) < DateTimeOffset.Now)
                                        {
                                            this.logger.Error("[TVHclient] Change during 'EndDate' calculation: set retention value from '" + retentionInDays + "' to '365' days");
                                            sti.EndDate = DateTimeOffset.Now.AddDays(365).ToUniversalTime();
                                        }
                                        else
                                        {
                                            sti.EndDate = DateTimeOffset.Now.AddDays(retentionInDays).ToUniversalTime();
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    this.logger.Error("[TVHclient] Exception during 'EndDate' calculation: " + e.Message + "\n" + e + "\n" + m);
                                }

                                try
                                {
                                    if (m.ContainsField("channel"))
                                    {
                                        sti.ChannelId = string.Empty + m.GetInt("channel");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("startExtra"))
                                    {
                                        sti.PrePaddingSeconds = (int)m.GetLong("startExtra") * 60;
                                        sti.IsPrePaddingRequired = true;
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("stopExtra"))
                                    {
                                        sti.PostPaddingSeconds = (int)m.GetLong("stopExtra") * 60;
                                        sti.IsPostPaddingRequired = true;
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("title"))
                                    {
                                        sti.Name = m.GetString("title");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("description"))
                                    {
                                        sti.Overview = m.GetString("description");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("priority"))
                                    {
                                        sti.Priority = m.GetInt("priority");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                try
                                {
                                    if (m.ContainsField("title"))
                                    {
                                        sti.SeriesId = m.GetString("title");
                                    }
                                }
                                catch (InvalidCastException)
                                {
                                }

                                /*
                                        public string ProgramId { get; set; }
                                        public bool RecordAnyChannel { get; set; }
                                        public bool RecordAnyTime { get; set; }
                                        public bool RecordNewOnly { get; set; }
                                 */
                                result.Add(sti);
                            }

                            return result;
                        }
                    });
        }

        private List<DayOfWeek> GetDayOfWeekListFromInt(int daysOfWeek)
        {
            List<DayOfWeek> result = new List<DayOfWeek>();
            if ((daysOfWeek & 0x01) != 0)
            {
                result.Add(DayOfWeek.Monday);
            }

            if ((daysOfWeek & 0x02) != 0)
            {
                result.Add(DayOfWeek.Tuesday);
            }

            if ((daysOfWeek & 0x04) != 0)
            {
                result.Add(DayOfWeek.Wednesday);
            }

            if ((daysOfWeek & 0x08) != 0)
            {
                result.Add(DayOfWeek.Thursday);
            }

            if ((daysOfWeek & 0x10) != 0)
            {
                result.Add(DayOfWeek.Friday);
            }

            if ((daysOfWeek & 0x20) != 0)
            {
                result.Add(DayOfWeek.Saturday);
            }

            if ((daysOfWeek & 0x40) != 0)
            {
                result.Add(DayOfWeek.Sunday);
            }

            return result;
        }

        public static int GetDaysOfWeekFromList(List<DayOfWeek> days)
        {
            int result = 0;
            foreach (DayOfWeek currDay in days)
            {
                switch (currDay)
                {
                    case DayOfWeek.Monday:
                        result = result | 0x1;
                        break;
                    case DayOfWeek.Tuesday:
                        result = result | 0x2;
                        break;
                    case DayOfWeek.Wednesday:
                        result = result | 0x4;
                        break;
                    case DayOfWeek.Thursday:
                        result = result | 0x8;
                        break;
                    case DayOfWeek.Friday:
                        result = result | 0x10;
                        break;
                    case DayOfWeek.Saturday:
                        result = result | 0x20;
                        break;
                    case DayOfWeek.Sunday:
                        result = result | 0x40;
                        break;
                }
            }

            return result;
        }

        public static int GetMinutesFromMidnight(DateTimeOffset time)
        {
            var utcTime = time.ToUniversalTime();
            int hours = utcTime.Hour;
            int minute = utcTime.Minute;
            int minutes = hours * 60 + minute;
            return minutes;
        }
    }
}