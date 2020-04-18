namespace TVHeadEnd.DataHelper
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Controller.LiveTv;
    using MediaBrowser.Model.LiveTv;
    using MediaBrowser.Model.Logging;

    using TVHeadEnd.HTSP;

    public class ChannelDataHelper
    {
        private readonly ILogger logger;
        private readonly TunerDataHelper tunerDataHelper;
        private readonly Dictionary<int, HtsMessage> data;
        private readonly Dictionary<string, string> piconData;
        private string channelType4Other = "Ignore";

        public ChannelDataHelper(ILogger logger, TunerDataHelper tunerDataHelper)
        {
            this.logger = logger;
            this.tunerDataHelper = tunerDataHelper;

            this.data = new Dictionary<int, HtsMessage>();
            this.piconData = new Dictionary<string, string>();
        }

        public ChannelDataHelper(ILogger logger)
            : this(logger, null)
        {
        }

        public void SetChannelType4Other(string channelType4Other)
        {
            this.channelType4Other = channelType4Other;
        }

        public void Clean()
        {
            lock (this.data)
            {
                this.data.Clear();
                if (this.tunerDataHelper != null)
                {
                    this.tunerDataHelper.Clean();
                }
            }
        }

        public void Add(HtsMessage message)
        {
            if (this.tunerDataHelper != null)
            {
                // TVHeadend don't send the information we need 
                // _tunerDataHelper.addTunerInfo(message);
            }

            lock (this.data)
            {
                try
                {
                    int channelId = message.GetInt("channelId");
                    if (this.data.ContainsKey(channelId))
                    {
                        HtsMessage storedMessage = this.data[channelId];
                        if (storedMessage != null)
                        {
                            foreach (KeyValuePair<string, object> entry in message)
                            {
                                if (storedMessage.ContainsField(entry.Key))
                                {
                                    storedMessage.RemoveField(entry.Key);
                                }

                                storedMessage.PutField(entry.Key, entry.Value);
                            }
                        }
                        else
                        {
                            this.logger.Error("[TVHclient] ChannelDataHelper: update for channelID '" + channelId + "' but no initial data found!");
                        }
                    }
                    else
                    {
                        if (message.ContainsField("channelNumber") && message.GetInt("channelNumber") >= 0)
                        {
                            // use only channels with number > 0
                            this.data.Add(channelId, message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logger.Error("[TVHclient] ChannelDataHelper.Add caught exception: " + ex.Message + "\nHTSmessage=" + message);
                }
            }
        }

        public string GetChannelIcon4ChannelId(string channelId)
        {
            string result;
            if (this.piconData.TryGetValue(channelId, out result))
            {
                return result;
            }

            return result;
        }

        public Task<IEnumerable<ChannelInfo>> BuildChannelInfos(CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew<IEnumerable<ChannelInfo>>(
                () =>
                    {
                        lock (this.data)
                        {
                            List<ChannelInfo> result = new List<ChannelInfo>();
                            foreach (KeyValuePair<int, HtsMessage> entry in this.data)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    this.logger.Info("[TVHclient] ChannelDataHelper.buildChannelInfos, call canceled - returning part list.");
                                    return result;
                                }

                                HtsMessage m = entry.Value;

                                try
                                {
                                    ChannelInfo ci = new ChannelInfo();
                                    ci.Id = string.Empty + entry.Key;

                                    ci.ImagePath = string.Empty;

                                    if (m.ContainsField("channelIcon"))
                                    {
                                        string channelIcon = m.GetString("channelIcon");
                                        Uri uriResult;
                                        bool uriCheckResult = Uri.TryCreate(channelIcon, UriKind.Absolute, out uriResult) && uriResult.Scheme == Uri.UriSchemeHttp;
                                        if (uriCheckResult)
                                        {
                                            ci.ImageUrl = channelIcon;
                                        }
                                        else
                                        {
                                            ci.HasImage = true;
                                            if (!this.piconData.ContainsKey(ci.Id))
                                            {
                                                this.piconData.Add(ci.Id, channelIcon);
                                            }
                                        }
                                    }

                                    if (m.ContainsField("channelName"))
                                    {
                                        string name = m.GetString("channelName");
                                        if (string.IsNullOrEmpty(name))
                                        {
                                            continue;
                                        }

                                        ci.Name = m.GetString("channelName");
                                    }

                                    if (m.ContainsField("channelNumber"))
                                    {
                                        int channelNumber = m.GetInt("channelNumber");
                                        ci.Number = string.Empty + channelNumber;
                                        if (m.ContainsField("channelNumberMinor"))
                                        {
                                            int channelNumberMinor = m.GetInt("channelNumberMinor");
                                            ci.Number = ci.Number + "." + channelNumberMinor;
                                        }
                                    }

                                    bool serviceFound = false;
                                    if (m.ContainsField("services"))
                                    {
                                        IList tunerInfoList = m.GetList("services");
                                        if (tunerInfoList != null && tunerInfoList.Count > 0)
                                        {
                                            HtsMessage firstServiceInList = (HtsMessage)tunerInfoList[0];
                                            if (firstServiceInList.ContainsField("type"))
                                            {
                                                string type = firstServiceInList.GetString("type").ToLower();
                                                switch (type)
                                                {
                                                    case "radio":
                                                        ci.ChannelType = ChannelType.Radio;
                                                        serviceFound = true;
                                                        break;
                                                    case "sdtv":
                                                    case "hdtv":
                                                    case "uhdtv":
                                                        ci.ChannelType = ChannelType.TV;
                                                        serviceFound = true;
                                                        break;
                                                    case "other":
                                                        switch (this.channelType4Other.ToLower())
                                                        {
                                                            case "tv":
                                                                this.logger.Info("[TVHclient] ChannelDataHelper: map service tag 'Other' to 'TV'.");
                                                                ci.ChannelType = ChannelType.TV;
                                                                serviceFound = true;
                                                                break;
                                                            case "radio":
                                                                this.logger.Info("[TVHclient] ChannelDataHelper: map service tag 'Other' to 'Radio'.");
                                                                ci.ChannelType = ChannelType.Radio;
                                                                serviceFound = true;
                                                                break;
                                                            default:
                                                                this.logger.Info("[TVHclient] ChannelDataHelper: don't map service tag 'Other' - will be ignored.");
                                                                break;
                                                        }

                                                        break;
                                                    default:
                                                        this.logger.Info("[TVHclient] ChannelDataHelper: unkown service tag '" + type + "' - will be ignored.");
                                                        break;
                                                }
                                            }
                                        }
                                    }

                                    if (!serviceFound)
                                    {
                                        this.logger.Info("[TVHclient] ChannelDataHelper: unable to detect service-type (tvheadend tag!!!) from service list:" + m);
                                        continue;
                                    }

                                    this.logger.Info("[TVHclient] ChannelDataHelper: Adding channel \n" + m);

                                    result.Add(ci);
                                }
                                catch (Exception ex)
                                {
                                    this.logger.Error("[TVHclient] ChannelDataHelper.BuildChannelInfos caught exception: " + ex.Message + "\nHTSmessage=" + m);
                                }
                            }

                            return result;
                        }
                    });
        }
    }
}