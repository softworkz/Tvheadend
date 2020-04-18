namespace TVHeadEnd
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Controller.Drawing;
    using MediaBrowser.Controller.LiveTv;
    using MediaBrowser.Controller.MediaEncoding;
    using MediaBrowser.Model.Dto;
    using MediaBrowser.Model.Entities;
    using MediaBrowser.Model.LiveTv;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.MediaInfo;

    using TVHeadEnd.Configuration;
    using TVHeadEnd.HTSP;
    using TVHeadEnd.HTSP.Responses;
    using TVHeadEnd.TimeoutHelper;

    public class LiveTvService
    {
        ////: ILiveTvService
        public event EventHandler DataSourceChanged;
        public event EventHandler<RecordingStatusChangedEventArgs> RecordingStatusChanged;

        // Added for stream probing
        private readonly IMediaEncoder mediaEncoder;

        private readonly TimeSpan timeout = TimeSpan.FromMinutes(5);

        private readonly HtsConnectionHandler htsConnectionHandler;
        private volatile int subscriptionId;

        private readonly ILogger logger;
        public DateTimeOffset LastRecordingChange = DateTimeOffset.MinValue;

        public LiveTvService(TvHeadendTunerConfig tunerConfig, ILogger logger, IMediaEncoder mediaEncoder)
        {
            // System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            logger.Info("[TVHclient] LiveTvService()");

            this.logger = logger;

            this.htsConnectionHandler = new HtsConnectionHandler(this.logger, tunerConfig);
            ////_htsConnectionHandler.SetLiveTvService(this);

            // Added for stream probing
            this.mediaEncoder = mediaEncoder;
        }

        public string HomePageUrl
        {
            get
            {
                return "http://tvheadend.org/";
            }
        }

        public string Name
        {
            get
            {
                return "TVHclient LiveTvService";
            }
        }

        public void SendDataSourceChanged()
        {
            try
            {
                if (this.DataSourceChanged != null)
                {
                    this.logger.Info("[TVHclient] sendDataSourceChanged called and calling EventHandler 'DataSourceChanged'");
                    this.DataSourceChanged(this, EventArgs.Empty);
                }
                else
                {
                    this.logger.Fatal("[TVHclient] sendDataSourceChanged called but EventHandler 'DataSourceChanged' was not set by Emby!!!");
                }
            }
            catch (Exception ex)
            {
                this.logger.Error("[TVHclient] LiveTvService.sendDataSourceChanged caught exception: " + ex.Message);
            }
        }

        public void SendRecordingStatusChanged(RecordingStatusChangedEventArgs recordingStatusChangedEventArgs)
        {
            try
            {
                this.logger.Fatal("[TVHclient] sendRecordingStatusChanged 1");
                if (this.RecordingStatusChanged != null)
                {
                    this.logger.Fatal("[TVHclient] sendRecordingStatusChanged 2");
                    this.RecordingStatusChanged(this, recordingStatusChangedEventArgs);
                }
                else
                {
                    this.logger.Fatal("[TVHclient] sendRecordingStatusChanged called but EventHandler 'RecordingStatusChanged' was not set by Emby!!!");
                }
            }
            catch (Exception ex)
            {
                this.logger.Error("[TVHclient] LiveTvService.sendRecordingStatusChanged caught exception: " + ex.Message);
            }
        }

        public async Task CancelSeriesTimerAsync(string timerId, CancellationToken cancellationToken)
        {
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] CancelSeriesTimerAsync, call canceled or timed out.");
                return;
            }

            HtsMessage deleteAutorecMessage = new HtsMessage();
            deleteAutorecMessage.Method = "deleteAutorecEntry";
            deleteAutorecMessage.PutField("id", timerId);

            TaskWithTimeoutRunner<HtsMessage> twtr = new TaskWithTimeoutRunner<HtsMessage>(this.timeout);
            TaskWithTimeoutResult<HtsMessage> twtRes = await twtr.RunWithTimeout(
                                                           Task.Factory.StartNew<HtsMessage>(
                                                               () =>
                                                                   {
                                                                       LoopBackResponseHandler lbrh = new LoopBackResponseHandler();
                                                                       this.htsConnectionHandler.SendMessage(deleteAutorecMessage, lbrh);
                                                                       this.LastRecordingChange = DateTimeOffset.UtcNow;
                                                                       return lbrh.GetResponse();
                                                                   }));

            if (twtRes.HasTimeout)
            {
                this.logger.Error("[TVHclient] Can't delete recording because of timeout");
            }
            else
            {
                HtsMessage deleteAutorecResponse = twtRes.Result;
                bool success = deleteAutorecResponse.GetInt("success", 0) == 1;
                if (!success)
                {
                    if (deleteAutorecResponse.ContainsField("error"))
                    {
                        this.logger.Error("[TVHclient] Can't delete recording: '" + deleteAutorecResponse.GetString("error") + "'");
                    }
                    else if (deleteAutorecResponse.ContainsField("noaccess"))
                    {
                        this.logger.Error("[TVHclient] Can't delete recording: '" + deleteAutorecResponse.GetString("noaccess") + "'");
                    }
                }
            }
        }

        public async Task CancelTimerAsync(string timerId, CancellationToken cancellationToken)
        {
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] CancelTimerAsync, call canceled or timed out.");
                return;
            }

            HtsMessage cancelTimerMessage = new HtsMessage();
            cancelTimerMessage.Method = "cancelDvrEntry";
            cancelTimerMessage.PutField("id", timerId);

            TaskWithTimeoutRunner<HtsMessage> twtr = new TaskWithTimeoutRunner<HtsMessage>(this.timeout);
            TaskWithTimeoutResult<HtsMessage> twtRes = await twtr.RunWithTimeout(
                                                           Task.Factory.StartNew<HtsMessage>(
                                                               () =>
                                                                   {
                                                                       LoopBackResponseHandler lbrh = new LoopBackResponseHandler();
                                                                       this.htsConnectionHandler.SendMessage(cancelTimerMessage, lbrh);
                                                                       this.LastRecordingChange = DateTimeOffset.UtcNow;
                                                                       return lbrh.GetResponse();
                                                                   }));

            if (twtRes.HasTimeout)
            {
                this.logger.Error("[TVHclient] Can't cancel timer because of timeout");
            }
            else
            {
                HtsMessage cancelTimerResponse = twtRes.Result;
                bool success = cancelTimerResponse.GetInt("success", 0) == 1;
                if (!success)
                {
                    if (cancelTimerResponse.ContainsField("error"))
                    {
                        this.logger.Error("[TVHclient] Can't cancel timer: '" + cancelTimerResponse.GetString("error") + "'");
                    }
                    else if (cancelTimerResponse.ContainsField("noaccess"))
                    {
                        this.logger.Error("[TVHclient] Can't cancel timer: '" + cancelTimerResponse.GetString("noaccess") + "'");
                    }
                }
            }
        }

        public async Task CloseLiveStream(string subscriptionId, CancellationToken cancellationToken)
        {
            await Task.Factory.StartNew<string>(
                () =>
                    {
                        // _logger.Info("[TVHclient] CloseLiveStream for subscriptionId = " + subscriptionId);
                        return subscriptionId;
                    });
        }

        public async Task CreateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
        {
            // Dummy method to avoid warnings
            await Task.Factory.StartNew<int>(() => { return 0; });

            throw new NotImplementedException();

            // int timeOut = await WaitForInitialLoadTask(cancellationToken);
            // if (!loaded || cancellationToken.IsCancellationRequested)
            // {
            // _logger.Info("[TVHclient] CreateSeriesTimerAsync, call canceled or timed out - returning empty list.");
            // return;
            // }

            ////_logger.Info("[TVHclient] CreateSeriesTimerAsync: got SeriesTimerInfo: " + dump(info));

            // HTSMessage createSeriesTimerMessage = new HTSMessage();
            // createSeriesTimerMessage.Method = "addAutorecEntry";
            // createSeriesTimerMessage.putField("title", info.Name);
            // if (!info.RecordAnyChannel)
            // {
            // createSeriesTimerMessage.putField("channelId", info.ChannelId);
            // }
            // createSeriesTimerMessage.putField("minDuration", 0);
            // createSeriesTimerMessage.putField("maxDuration", 0);

            // int tempPriority = info.Priority;
            // if (tempPriority == 0)
            // {
            // tempPriority = _priority; // info.Priority delivers 0 if timers is newly created - no GUI
            // }
            // createSeriesTimerMessage.putField("priority", tempPriority);
            // createSeriesTimerMessage.putField("configName", _profile);
            // createSeriesTimerMessage.putField("daysOfWeek", AutorecDataHelper.getDaysOfWeekFromList(info.Days));

            // if (!info.RecordAnyTime)
            // {
            // createSeriesTimerMessage.putField("approxTime", AutorecDataHelper.getMinutesFromMidnight(info.StartDate));
            // }
            // createSeriesTimerMessage.putField("startExtra", (long)(info.PrePaddingSeconds / 60L));
            // createSeriesTimerMessage.putField("stopExtra", (long)(info.PostPaddingSeconds / 60L));
            // createSeriesTimerMessage.putField("comment", info.Overview);

            ////_logger.Info("[TVHclient] CreateSeriesTimerAsync: created HTSP message: " + createSeriesTimerMessage.ToString());

            ///*
            // public DateTime EndDate { get; set; }
            // public string ProgramId { get; set; }
            // public bool RecordNewOnly { get; set; } 
            // */

            ////HTSMessage createSeriesTimerResponse = await Task.Factory.StartNew<HTSMessage>(() =>
            ////{
            ////    LoopBackResponseHandler lbrh = new LoopBackResponseHandler();
            ////    _htsConnection.sendMessage(createSeriesTimerMessage, lbrh);
            ////    return lbrh.getResponse();
            ////});

            // TaskWithTimeoutRunner<HTSMessage> twtr = new TaskWithTimeoutRunner<HTSMessage>(TIMEOUT);
            // TaskWithTimeoutResult<HTSMessage> twtRes = await  twtr.RunWithTimeout(Task.Factory.StartNew<HTSMessage>(() =>
            // {
            // LoopBackResponseHandler lbrh = new LoopBackResponseHandler();
            // _htsConnection.sendMessage(createSeriesTimerMessage, lbrh);
            // return lbrh.getResponse();
            // }));

            // if (twtRes.HasTimeout)
            // {
            // _logger.Error("[TVHclient] Can't create series because of timeout");
            // }
            // else
            // {
            // HTSMessage createSeriesTimerResponse = twtRes.Result;
            // Boolean success = createSeriesTimerResponse.getInt("success", 0) == 1;
            // if (!success)
            // {
            // _logger.Error("[TVHclient] Can't create series timer: '" + createSeriesTimerResponse.getString("error") + "'");
            // }
            // }
        }

        public async Task CreateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
        {
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] CreateTimerAsync, call canceled or timed out.");
                return;
            }

            HtsMessage createTimerMessage = new HtsMessage();
            createTimerMessage.Method = "addDvrEntry";
            createTimerMessage.PutField("channelId", info.ChannelId);
            createTimerMessage.PutField("start", info.StartDate.ToUnixTimeSeconds());
            createTimerMessage.PutField("stop", info.EndDate.ToUnixTimeSeconds());
            createTimerMessage.PutField("startExtra", (long)(info.PrePaddingSeconds / 60));
            createTimerMessage.PutField("stopExtra", (long)(info.PostPaddingSeconds / 60));
            createTimerMessage.PutField("priority", this.htsConnectionHandler.Configuration.Priority); // info.Priority delivers always 0 - no GUI
            createTimerMessage.PutField("configName", this.htsConnectionHandler.Configuration.Profile);
            createTimerMessage.PutField("description", info.Overview);
            createTimerMessage.PutField("title", info.Name);
            createTimerMessage.PutField("creator", nameof(TunerProviderTvHeadend));

            TaskWithTimeoutRunner<HtsMessage> twtr = new TaskWithTimeoutRunner<HtsMessage>(this.timeout);
            TaskWithTimeoutResult<HtsMessage> twtRes = await twtr.RunWithTimeout(
                                                           Task.Factory.StartNew<HtsMessage>(
                                                               () =>
                                                                   {
                                                                       LoopBackResponseHandler lbrh = new LoopBackResponseHandler();
                                                                       this.htsConnectionHandler.SendMessage(createTimerMessage, lbrh);
                                                                       return lbrh.GetResponse();
                                                                   }));

            if (twtRes.HasTimeout)
            {
                this.logger.Error("[TVHclient] Can't create timer because of timeout");
            }
            else
            {
                HtsMessage createTimerResponse = twtRes.Result;
                bool success = createTimerResponse.GetInt("success", 0) == 1;
                if (!success)
                {
                    if (createTimerResponse.ContainsField("error"))
                    {
                        this.logger.Error("[TVHclient] Can't create timer: '" + createTimerResponse.GetString("error") + "'");
                    }
                    else if (createTimerResponse.ContainsField("noaccess"))
                    {
                        this.logger.Error("[TVHclient] Can't create timer: '" + createTimerResponse.GetString("noaccess") + "'");
                    }
                }
            }
        }

        public async Task DeleteRecordingAsync(string recordingId, CancellationToken cancellationToken)
        {
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] DeleteRecordingAsync, call canceled or timed out.");
                return;
            }

            HtsMessage deleteRecordingMessage = new HtsMessage();
            deleteRecordingMessage.Method = "deleteDvrEntry";
            deleteRecordingMessage.PutField("id", recordingId);

            TaskWithTimeoutRunner<HtsMessage> twtr = new TaskWithTimeoutRunner<HtsMessage>(this.timeout);
            TaskWithTimeoutResult<HtsMessage> twtRes = await twtr.RunWithTimeout(
                                                           Task.Factory.StartNew<HtsMessage>(
                                                               () =>
                                                                   {
                                                                       LoopBackResponseHandler lbrh = new LoopBackResponseHandler();
                                                                       this.htsConnectionHandler.SendMessage(deleteRecordingMessage, lbrh);
                                                                       this.LastRecordingChange = DateTimeOffset.UtcNow;
                                                                       return lbrh.GetResponse();
                                                                   }));

            if (twtRes.HasTimeout)
            {
                this.logger.Error("[TVHclient] Can't delete recording because of timeout");
            }
            else
            {
                HtsMessage deleteRecordingResponse = twtRes.Result;
                bool success = deleteRecordingResponse.GetInt("success", 0) == 1;
                if (!success)
                {
                    if (deleteRecordingResponse.ContainsField("error"))
                    {
                        this.logger.Error("[TVHclient] Can't delete recording: '" + deleteRecordingResponse.GetString("error") + "'");
                    }
                    else if (deleteRecordingResponse.ContainsField("noaccess"))
                    {
                        this.logger.Error("[TVHclient] Can't delete recording: '" + deleteRecordingResponse.GetString("noaccess") + "'");
                    }
                }
            }
        }

        public Task<ImageStream> GetChannelImageAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ImageStream>(this.htsConnectionHandler.GetChannelImage(channelId, cancellationToken));
        }

        public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken)
        {
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] GetChannelsAsync, call canceled or timed out - returning empty list.");
                return new List<ChannelInfo>();
            }

            TaskWithTimeoutRunner<IEnumerable<ChannelInfo>> twtr = new TaskWithTimeoutRunner<IEnumerable<ChannelInfo>>(this.timeout);
            TaskWithTimeoutResult<IEnumerable<ChannelInfo>> twtRes = await
                                                                         twtr.RunWithTimeout(this.htsConnectionHandler.BuildChannelInfos(cancellationToken));

            if (twtRes.HasTimeout)
            {
                return new List<ChannelInfo>();
            }

            var list = twtRes.Result.ToList();

            foreach (var channel in list)
            {
                if (string.IsNullOrEmpty(channel.ImageUrl))
                {
                    channel.ImageUrl = this.htsConnectionHandler.GetChannelImageUrl(channel.Id);
                }
            }

            return list;
        }

        public async Task<MediaSourceInfo> GetChannelStream(string channelId, string mediaSourceId, CancellationToken cancellationToken)
        {
            HtsMessage getTicketMessage = new HtsMessage();
            getTicketMessage.Method = "getTicket";
            getTicketMessage.PutField("channelId", channelId);

            TaskWithTimeoutRunner<HtsMessage> twtr = new TaskWithTimeoutRunner<HtsMessage>(this.timeout);
            TaskWithTimeoutResult<HtsMessage> twtRes = await twtr.RunWithTimeout(
                                                           Task.Factory.StartNew<HtsMessage>(
                                                               () =>
                                                                   {
                                                                       LoopBackResponseHandler lbrh = new LoopBackResponseHandler();
                                                                       this.htsConnectionHandler.SendMessage(getTicketMessage, lbrh);
                                                                       return lbrh.GetResponse();
                                                                   }));

            if (twtRes.HasTimeout)
            {
                this.logger.Error("[TVHclient] Timeout obtaining playback authentication ticket from TVH");
            }
            else
            {
                HtsMessage getTicketResponse = twtRes.Result;

                if (this.subscriptionId == int.MaxValue)
                {
                    this.subscriptionId = 0;
                }

                int currSubscriptionId = this.subscriptionId++;

                if (this.htsConnectionHandler.Configuration.EnableSubsMaudios)
                {
                    this.logger.Info("[TVHclient] Support for live TV subtitles and multiple audio tracks is enabled.");

                    MediaSourceInfo livetvasset = new MediaSourceInfo();

                    livetvasset.Id = string.Empty + currSubscriptionId;

                    // Use HTTP basic auth in HTTP header instead of TVH ticketing system for authentication to allow the users to switch subs or audio tracks at any time
                    livetvasset.Path = this.htsConnectionHandler.HttpBaseUrl + getTicketResponse.GetString("path");
                    livetvasset.Protocol = MediaProtocol.Http;
                    livetvasset.RequiredHttpHeaders = this.htsConnectionHandler.GetHeaders();

                    // Probe the asset stream to determine available sub-streams
                    string livetvassetProbeUrl = string.Empty + livetvasset.Path;
                    string livetvassetSource = "LiveTV";

                    // If enabled, force video deinterlacing for channels
                    if (this.htsConnectionHandler.Configuration.ForceDeinterlace)
                    {
                        this.logger.Info("[TVHclient] Force video deinterlacing for all channels and recordings is enabled.");

                        foreach (MediaStream i in livetvasset.MediaStreams)
                        {
                            if (i.Type == MediaStreamType.Video && i.IsInterlaced == false)
                            {
                                i.IsInterlaced = true;
                            }
                        }
                    }

                    return livetvasset;
                }
                else
                {
                    return new MediaSourceInfo
                               {
                                   Id = string.Empty + currSubscriptionId,
                                   Path = this.htsConnectionHandler.HttpBaseUrl + getTicketResponse.GetString("path") + "?ticket=" + getTicketResponse.GetString("ticket"),
                                   Protocol = MediaProtocol.Http,
                                   MediaStreams = new List<MediaStream>
                                                      {
                                                          new MediaStream
                                                              {
                                                                  Type = MediaStreamType.Video,

                                                                  // Set the index to -1 because we don't know the exact index of the video stream within the container
                                                                  Index = -1,

                                                                  // Set to true if unknown to enable deinterlacing
                                                                  IsInterlaced = true
                                                              },
                                                          new MediaStream
                                                              {
                                                                  Type = MediaStreamType.Audio,

                                                                  // Set the index to -1 because we don't know the exact index of the audio stream within the container
                                                                  Index = -1
                                                              }
                                                      }
                               };
                }
            }

            throw new TimeoutException(string.Empty);
        }

        public Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(string channelId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<SeriesTimerInfo> GetNewTimerDefaultsAsync(CancellationToken cancellationToken, ProgramInfo program = null)
        {
            return await Task.Factory.StartNew<SeriesTimerInfo>(
                       () =>
                           {
                               return new SeriesTimerInfo
                                          {
                                              PostPaddingSeconds = 0,
                                              PrePaddingSeconds = 0,
                                              RecordAnyChannel = true,
                                              RecordAnyTime = true,
                                              RecordNewOnly = false
                                          };
                           });
        }

        public Task<ImageStream> GetProgramImageAsync(string programId, string channelId, CancellationToken cancellationToken)
        {
            // Leave as is. This is handled by supplying image url to ProgramInfo
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId, DateTimeOffset startDateUtc, DateTimeOffset endDateUtc, CancellationToken cancellationToken)
        {
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] GetProgramsAsync, call canceled or timed out - returning empty list.");
                return new List<ProgramInfo>();
            }

            GetEventsResponseHandler currGetEventsResponseHandler = new GetEventsResponseHandler(startDateUtc, endDateUtc, this.logger, cancellationToken);

            HtsMessage queryEvents = new HtsMessage();
            queryEvents.Method = "getEvents";
            queryEvents.PutField("channelId", Convert.ToInt32(channelId));
            queryEvents.PutField("maxTime", endDateUtc.ToUnixTimeSeconds());
            this.htsConnectionHandler.SendMessage(queryEvents, currGetEventsResponseHandler);

            this.logger.Info("[TVHclient] GetProgramsAsync, ask TVH for events of channel '" + channelId + "'.");

            TaskWithTimeoutRunner<IEnumerable<ProgramInfo>> twtr = new TaskWithTimeoutRunner<IEnumerable<ProgramInfo>>(this.timeout);
            TaskWithTimeoutResult<IEnumerable<ProgramInfo>> twtRes = await
                                                                         twtr.RunWithTimeout(currGetEventsResponseHandler.GetEvents(cancellationToken, channelId));

            if (twtRes.HasTimeout)
            {
                this.logger.Info("[TVHclient] GetProgramsAsync, timeout during call for events of channel '" + channelId + "'.");
                return new List<ProgramInfo>();
            }

            return twtRes.Result;
        }

        public Task<ImageStream> GetRecordingImageAsync(string recordingId, CancellationToken cancellationToken)
        {
            // Leave as is. This is handled by supplying image url to RecordingInfo
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<RecordingInfo>> GetRecordingsAsync(CancellationToken cancellationToken)
        {
            return new List<RecordingInfo>();
        }

        public async Task<IEnumerable<MyRecordingInfo>> GetAllRecordingsAsync(CancellationToken cancellationToken)
        {
            // retrieve all 'Pending', 'Inprogress' and 'Completed' recordings
            // we don't deliver the 'Pending' recordings
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] GetRecordingsAsync, call canceled or timed out - returning empty list.");
                return new List<MyRecordingInfo>();
            }

            TaskWithTimeoutRunner<IEnumerable<MyRecordingInfo>> twtr = new TaskWithTimeoutRunner<IEnumerable<MyRecordingInfo>>(this.timeout);
            TaskWithTimeoutResult<IEnumerable<MyRecordingInfo>> twtRes = await
                                                                             twtr.RunWithTimeout(this.htsConnectionHandler.BuildDvrInfos(cancellationToken));

            if (twtRes.HasTimeout)
            {
                return new List<MyRecordingInfo>();
            }

            return twtRes.Result;
        }

        private void LogStringList(List<string> theList, string prefix)
        {
            theList.ForEach(delegate(string s) { this.logger.Info(prefix + s); });
        }

        public async Task<MediaSourceInfo> GetRecordingStream(string recordingId, string mediaSourceId, CancellationToken cancellationToken)
        {
            HtsMessage getTicketMessage = new HtsMessage();
            getTicketMessage.Method = "getTicket";
            getTicketMessage.PutField("dvrId", recordingId);

            TaskWithTimeoutRunner<HtsMessage> twtr = new TaskWithTimeoutRunner<HtsMessage>(this.timeout);
            TaskWithTimeoutResult<HtsMessage> twtRes = await twtr.RunWithTimeout(
                                                           Task.Factory.StartNew<HtsMessage>(
                                                               () =>
                                                                   {
                                                                       LoopBackResponseHandler lbrh = new LoopBackResponseHandler();
                                                                       this.htsConnectionHandler.SendMessage(getTicketMessage, lbrh);
                                                                       return lbrh.GetResponse();
                                                                   }));

            if (twtRes.HasTimeout)
            {
                this.logger.Error("[TVHclient] Timeout obtaining playback authentication ticket from TVH");
            }
            else
            {
                HtsMessage getTicketResponse = twtRes.Result;

                if (this.subscriptionId == int.MaxValue)
                {
                    this.subscriptionId = 0;
                }

                int currSubscriptionId = this.subscriptionId++;

                if (this.htsConnectionHandler.Configuration.EnableSubsMaudios)
                {
                    this.logger.Info("[TVHclient] Support for live TV subtitles and multiple audio tracks is enabled.");

                    MediaSourceInfo recordingasset = new MediaSourceInfo();

                    recordingasset.Id = string.Empty + currSubscriptionId;

                    // Use HTTP basic auth instead of TVH ticketing system for authentication to allow the users to switch subs or audio tracks at any time
                    recordingasset.Path = this.htsConnectionHandler.HttpBaseUrl + getTicketResponse.GetString("path");
                    recordingasset.Protocol = MediaProtocol.Http;

                    // Set asset source and type for stream probing and logging
                    string recordingassetProbeUrl = string.Empty + recordingasset.Path;
                    string recordingassetSource = "Recording";

                    // If enabled, force video deinterlacing for recordings
                    if (this.htsConnectionHandler.Configuration.ForceDeinterlace)
                    {
                        this.logger.Info("[TVHclient] Force video deinterlacing for all channels and recordings is enabled.");

                        foreach (MediaStream i in recordingasset.MediaStreams)
                        {
                            if (i.Type == MediaStreamType.Video && i.IsInterlaced == false)
                            {
                                i.IsInterlaced = true;
                            }
                        }
                    }

                    return recordingasset;
                }
                else
                {
                    return new MediaSourceInfo
                               {
                                   Id = string.Empty + currSubscriptionId,
                                   Path = this.htsConnectionHandler.HttpBaseUrl + getTicketResponse.GetString("path") + "?ticket=" + getTicketResponse.GetString("ticket"),
                                   Protocol = MediaProtocol.Http,
                                   MediaStreams = new List<MediaStream>
                                                      {
                                                          new MediaStream
                                                              {
                                                                  Type = MediaStreamType.Video,

                                                                  // Set the index to -1 because we don't know the exact index of the video stream within the container
                                                                  Index = -1,

                                                                  // Set to true if unknown to enable deinterlacing
                                                                  IsInterlaced = true
                                                              },
                                                          new MediaStream
                                                              {
                                                                  Type = MediaStreamType.Audio,

                                                                  // Set the index to -1 because we don't know the exact index of the audio stream within the container
                                                                  Index = -1
                                                              }
                                                      }
                               };
                }
            }

            throw new TimeoutException();
        }

        public Task<List<MediaSourceInfo>> GetRecordingStreamMediaSources(string recordingId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimersAsync(CancellationToken cancellationToken)
        {
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] GetSeriesTimersAsync, call canceled ot timed out - returning empty list.");
                return new List<SeriesTimerInfo>();
            }

            TaskWithTimeoutRunner<IEnumerable<SeriesTimerInfo>> twtr = new TaskWithTimeoutRunner<IEnumerable<SeriesTimerInfo>>(this.timeout);
            TaskWithTimeoutResult<IEnumerable<SeriesTimerInfo>> twtRes = await
                                                                             twtr.RunWithTimeout(this.htsConnectionHandler.BuildAutorecInfos(cancellationToken));

            if (twtRes.HasTimeout)
            {
                return new List<SeriesTimerInfo>();
            }

            return twtRes.Result;
        }

        public async Task<LiveTvServiceStatusInfo> GetStatusInfoAsync(CancellationToken cancellationToken)
        {
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] GetStatusInfoAsync, call canceled or timed out.");
                return new LiveTvServiceStatusInfo
                           {
                               Status = LiveTvServiceStatus.Unavailable
                           };
            }

            string serverVersionMessage = null;
            var serverInfo = this.htsConnectionHandler.ServerInfo;

            if (serverInfo != null)
            {
                int usedHtsPversion = serverInfo.ServerProtocolVersion < (int)HtsMessage.HTSP_VERSION ? serverInfo.ServerProtocolVersion : (int)HtsMessage.HTSP_VERSION;

                serverVersionMessage = "<p>" + serverInfo.Servername + " " + serverInfo.Serverversion + "</p>"
                                              + "<p>HTSP protocol version: " + usedHtsPversion + "</p>"
                                              + "<p>Free diskspace: " + serverInfo.Diskspace + "</p>";
            }

            // TaskWithTimeoutRunner<List<LiveTvTunerInfo>> twtr = new TaskWithTimeoutRunner<List<LiveTvTunerInfo>>(TIMEOUT);
            // TaskWithTimeoutResult<List<LiveTvTunerInfo>> twtRes = await
            // twtr.RunWithTimeout(_tunerDataHelper.buildTunerInfos(cancellationToken));
            List<LiveTvTunerInfo> tvTunerInfos;

            // if (twtRes.HasTimeout)
            // {
            tvTunerInfos = new List<LiveTvTunerInfo>();

            // } else
            // {
            // tvTunerInfos = twtRes.Result;
            // }
            return new LiveTvServiceStatusInfo
                       {
                           Version = serverVersionMessage,
                           Tuners = tvTunerInfos,
                           Status = LiveTvServiceStatus.Ok,
                       };
        }

        public async Task<IEnumerable<TimerInfo>> GetTimersAsync(CancellationToken cancellationToken)
        {
            // retrieve the 'Pending' recordings");
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] GetTimersAsync, call canceled or timed out - returning empty list.");
                return new List<TimerInfo>();
            }

            TaskWithTimeoutRunner<IEnumerable<TimerInfo>> twtr = new TaskWithTimeoutRunner<IEnumerable<TimerInfo>>(this.timeout);
            TaskWithTimeoutResult<IEnumerable<TimerInfo>> twtRes = await
                                                                       twtr.RunWithTimeout(this.htsConnectionHandler.BuildPendingTimersInfos(cancellationToken));

            if (twtRes.HasTimeout)
            {
                return new List<TimerInfo>();
            }

            return twtRes.Result;
        }

        public Task RecordLiveStream(string id, CancellationToken cancellationToken)
        {
            this.logger.Info("[TVHclient] RecordLiveStream " + id);

            throw new NotImplementedException();
        }

        public Task ResetTuner(string id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
        {
            await this.CancelSeriesTimerAsync(info.Id, cancellationToken);
            this.LastRecordingChange = DateTimeOffset.UtcNow;

            // TODO add if method is implemented 
            // await CreateSeriesTimerAsync(info, cancellationToken);
        }

        public async Task UpdateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
        {
            var loaded = await this.WaitForInitialLoadTask(cancellationToken).ConfigureAwait(false);;
            if (!loaded || cancellationToken.IsCancellationRequested)
            {
                this.logger.Info("[TVHclient] UpdateTimerAsync, call canceled or timed out.");
                return;
            }

            HtsMessage updateTimerMessage = new HtsMessage();
            updateTimerMessage.Method = "updateDvrEntry";
            updateTimerMessage.PutField("id", info.Id);
            updateTimerMessage.PutField("startExtra", (long)(info.PrePaddingSeconds / 60));
            updateTimerMessage.PutField("stopExtra", (long)(info.PostPaddingSeconds / 60));

            TaskWithTimeoutRunner<HtsMessage> twtr = new TaskWithTimeoutRunner<HtsMessage>(this.timeout);
            TaskWithTimeoutResult<HtsMessage> twtRes = await twtr.RunWithTimeout(
                                                           Task.Factory.StartNew<HtsMessage>(
                                                               () =>
                                                                   {
                                                                       LoopBackResponseHandler lbrh = new LoopBackResponseHandler();
                                                                       this.htsConnectionHandler.SendMessage(updateTimerMessage, lbrh);
                                                                       this.LastRecordingChange = DateTimeOffset.UtcNow;
                                                                       return lbrh.GetResponse();
                                                                   }));

            if (twtRes.HasTimeout)
            {
                this.logger.Error("[TVHclient] Can't update timer because of timeout");
            }
            else
            {
                HtsMessage updateTimerResponse = twtRes.Result;
                bool success = updateTimerResponse.GetInt("success", 0) == 1;
                if (!success)
                {
                    if (updateTimerResponse.ContainsField("error"))
                    {
                        this.logger.Error("[TVHclient] Can't update timer: '" + updateTimerResponse.GetString("error") + "'");
                    }
                    else if (updateTimerResponse.ContainsField("noaccess"))
                    {
                        this.logger.Error("[TVHclient] Can't update timer: '" + updateTimerResponse.GetString("noaccess") + "'");
                    }
                }
            }
        }

        /***********/
        /* Helpers */
        /***********/
        private Task<bool> WaitForInitialLoadTask(CancellationToken cancellationToken)
        {
            return this.htsConnectionHandler.WaitForInitialLoadAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }

        private string Dump(SeriesTimerInfo sti)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\n<SeriesTimerInfo>\n");
            sb.Append("  Id:                    " + sti.Id + "\n");
            sb.Append("  Name:                  " + sti.Name + "\n");
            sb.Append("  Overview:              " + sti.Overview + "\n");
            sb.Append("  Priority:              " + sti.Priority + "\n");
            sb.Append("  ChannelId:             " + sti.ChannelId + "\n");
            sb.Append("  ProgramId:             " + sti.ProgramId + "\n");
            sb.Append("  Days:                  " + this.Dump(sti.Days) + "\n");
            sb.Append("  StartDate:             " + sti.StartDate + "\n");
            sb.Append("  EndDate:               " + sti.EndDate + "\n");
            sb.Append("  IsPrePaddingRequired:  " + sti.IsPrePaddingRequired + "\n");
            sb.Append("  PrePaddingSeconds:     " + sti.PrePaddingSeconds + "\n");
            sb.Append("  IsPostPaddingRequired: " + sti.IsPrePaddingRequired + "\n");
            sb.Append("  PostPaddingSeconds:    " + sti.PostPaddingSeconds + "\n");
            sb.Append("  RecordAnyChannel:      " + sti.RecordAnyChannel + "\n");
            sb.Append("  RecordAnyTime:         " + sti.RecordAnyTime + "\n");
            sb.Append("  RecordNewOnly:         " + sti.RecordNewOnly + "\n");
            sb.Append("</SeriesTimerInfo>\n");
            return sb.ToString();
        }

        private string Dump(List<DayOfWeek> days)
        {
            StringBuilder sb = new StringBuilder();
            foreach (DayOfWeek dow in days)
            {
                sb.Append(dow + ", ");
            }

            string tmpResult = sb.ToString();
            if (tmpResult.EndsWith(","))
            {
                tmpResult = tmpResult.Substring(0, tmpResult.Length - 2);
            }

            return tmpResult;
        }

        /*
        public async Task CopyFilesAsync(StreamReader source, StreamWriter destination)
        {
            char[] buffer = new char[0x1000];
            int numRead;
            while ((numRead = await source.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                await destination.WriteAsync(buffer, 0, numRead);
            }
        }
        */
    }
}