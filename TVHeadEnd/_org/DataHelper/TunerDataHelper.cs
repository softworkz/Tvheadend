namespace TVHeadEnd.DataHelper
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Controller.LiveTv;
    using MediaBrowser.Model.LiveTv;
    using MediaBrowser.Model.Logging;

    using TVHeadEnd.HTSP;

    public class TunerDataHelper
    {
        private readonly ILogger logger;
        private readonly Dictionary<string, HtsMessage> data;

        public TunerDataHelper(ILogger logger)
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

        public void AddTunerInfo(HtsMessage tunerMessage)
        {
            lock (this.data)
            {
                string channelId = string.Empty + tunerMessage.GetInt("channelId");
                if (this.data.ContainsKey(channelId))
                {
                    this.data.Remove(channelId);
                }

                this.data.Add(channelId, tunerMessage);
            }
        }

        /*
          <dump>
            channelId : 240
            channelNumber : 40
            channelName : zdf.kultur
            eventId : 11708150
            nextEventId : 11708152
            services :       name : CXD2837 DVB-C DVB-T/T2 (adapter 7)/KBW: 370,000 kHz/zdf.kultur
              type : SDTV
        ,       name : CXD2837 DVB-C DVB-T/T2  (adapter 6)/KBW: 370,000 kHz/zdf.kultur
              type : SDTV
        ,       name : STV0367 DVB-C DVB-T (adapter 5)/KBW: 370,000 kHz/zdf.kultur
              type : SDTV
        ,       name : STV0367 DVB-C DVB-T (adapter 4)/KBW: 370,000 kHz/zdf.kultur
              type : SDTV
        ,       name : CXD2837 DVB-C DVB-T/T2 (adapter 3)/KBW: 370,000 kHz/zdf.kultur
              type : SDTV
        ,       name : CXD2837 DVB-C DVB-T/T2 (adapter 2)/KBW: 370,000 kHz/zdf.kultur
              type : SDTV
        ,       name : STV0367 DVB-C DVB-T (adapter 1)/KBW: 370,000 kHz/zdf.kultur
              type : SDTV
        ,       name : STV0367 DVB-C DVB-T (adapter 0)/KBW: 370,000 kHz/zdf.kultur
              type : SDTV
        , 
            tags : 1, 2, 
            method : channelAdd
          </dump>
        */
        public Task<List<LiveTvTunerInfo>> BuildTunerInfos(CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew<List<LiveTvTunerInfo>>(
                () =>
                    {
                        List<LiveTvTunerInfo> result = new List<LiveTvTunerInfo>();
                        lock (this.data)
                        {
                            foreach (HtsMessage currMessage in this.data.Values)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    this.logger.Info("[TVHclient] TunerDataHelper.buildTunerInfos: cancel requst received. Returning only partly results");
                                    return result;
                                }

                                string channelId = string.Empty;
                                if (currMessage.ContainsField("channelId"))
                                {
                                    channelId = string.Empty + currMessage.GetInt("channelId");
                                }

                                string programName = string.Empty;
                                if (currMessage.ContainsField("channelName"))
                                {
                                    programName = currMessage.GetString("channelName");
                                }

                                IList services = null;
                                if (currMessage.ContainsField("services"))
                                {
                                    services = currMessage.GetList("services");
                                }

                                if (services != null)
                                {
                                    foreach (HtsMessage currService in services)
                                    {
                                        string name = string.Empty;
                                        if (currService.ContainsField("name"))
                                        {
                                            name = currService.GetString("name");
                                        }
                                        else
                                        {
                                            continue;
                                        }

                                        string type = string.Empty;
                                        if (currService.ContainsField("type"))
                                        {
                                            type = currService.GetString("type");
                                        }

                                        LiveTvTunerInfo ltti = new LiveTvTunerInfo();
                                        ltti.Id = name;
                                        ltti.Name = name;
                                        ltti.ProgramName = programName;
                                        ltti.SourceType = type;
                                        ltti.ChannelId = channelId;
                                        ltti.Status = LiveTvTunerStatus.Available;

                                        ltti.CanReset = false; // currently not possible

                                        // ltti.Clients // not available from TVheadend
                                        // ltti.RecordingId // not available from TVheadend
                                        result.Add(ltti);
                                    }
                                }
                            }
                        }

                        return result;
                    });
        }
    }
}