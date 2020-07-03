﻿namespace TVHeadEnd
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Common.Extensions;
    using MediaBrowser.Controller.Channels;
    using MediaBrowser.Controller.Entities;
    using MediaBrowser.Controller.LiveTv;
    using MediaBrowser.Controller.Providers;
    using MediaBrowser.Model.Channels;
    using MediaBrowser.Model.Dto;
    using MediaBrowser.Model.Entities;
    using MediaBrowser.Model.LiveTv;
    using MediaBrowser.Model.MediaInfo;

    using TVHeadEnd.Configuration;

    public class RecordingsChannel : IChannel, IHasCacheKey, ISupportsDelete, ISupportsLatestMedia, ISupportsMediaProbe, IHasFolderAttributes, IHasChangeEvent
    {
        private readonly ILiveTvManager liveTvManager;
        private readonly TvHeadendTunerConfig tunerConfig;
        private readonly string webRoot;
        private Timer updateTimer;

        public RecordingsChannel(ILiveTvManager liveTvManager, TvHeadendTunerConfig tunerConfig, string webRoot)
        {
            this.tunerConfig = tunerConfig;
            this.webRoot = webRoot;
            this.liveTvManager = liveTvManager;

            var interval = TimeSpan.FromMinutes(15);
            this.updateTimer = new Timer(this.OnUpdateTimerCallback, null, interval, interval);
        }

        public event EventHandler ContentChanged;

        public string[] Attributes
        {
            get
            {
                return new[] { "Recordings" };
            }
        }

        public string DataVersion
        {
            get
            {
                return "1";
            }
        }

        public string Description
        {
            get
            {
                return "TVHeadEnd Recordings";
            }
        }

        public string HomePageUrl
        {
            get
            {
                return "https://tvheadend.org";
            }
        }

        public string Name
        {
            get
            {
                return "TVHeadEnd Recordings";
            }
        }

        public ChannelParentalRating ParentalRating
        {
            get
            {
                return ChannelParentalRating.GeneralAudience;
            }
        }

        public bool CanDelete(BaseItem item)
        {
            return !item.IsFolder;
        }

        public Task DeleteItem(string id, CancellationToken cancellationToken)
        {
            return this.GetService().DeleteRecordingAsync(id, cancellationToken);
        }

        public void Dispose()
        {
            if (this.updateTimer != null)
            {
                this.updateTimer.Dispose();
                this.updateTimer = null;
            }
        }

        public string GetCacheKey(string userId)
        {
            var now = DateTimeOffset.UtcNow;

            var values = new List<string>();

            values.Add(now.DayOfYear.ToString(CultureInfo.InvariantCulture));
            values.Add(now.Hour.ToString(CultureInfo.InvariantCulture));

            double minute = now.Minute;
            minute /= 5;

            values.Add(Math.Floor(minute).ToString(CultureInfo.InvariantCulture));

            values.Add(this.GetService().LastRecordingChange.Ticks.ToString(CultureInfo.InvariantCulture));

            return string.Join("-", values.ToArray());
        }

        public InternalChannelFeatures GetChannelFeatures()
        {
            return new InternalChannelFeatures
                       {
                           ContentTypes = new List<ChannelMediaContentType>
                                              {
                                                  ChannelMediaContentType.Movie,
                                                  ChannelMediaContentType.Episode,
                                                  ChannelMediaContentType.Clip
                                              },
                           MediaTypes = new List<ChannelMediaType>
                                            {
                                                ChannelMediaType.Audio,
                                                ChannelMediaType.Video
                                            },
                           SupportsContentDownloading = true
                       };
        }

        public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        {
            if (type == ImageType.Primary)
            {
                return Task.FromResult(
                    new DynamicImageResponse
                        {
                            Path = "https://github.com/MediaBrowser/Tvheadend/raw/master/TVHeadEnd/Images/TVHeadEnd.png?raw=true",
                            Protocol = MediaProtocol.Http,
                            HasImage = true
                        });
            }

            return Task.FromResult(
                new DynamicImageResponse
                    {
                        HasImage = false
                    });
        }

        public Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query.FolderId))
            {
                return this.GetRecordingGroups(query, cancellationToken);
            }

            if (query.FolderId.StartsWith("series_", StringComparison.OrdinalIgnoreCase))
            {
                var hash = query.FolderId.Split('_')[1];
                return this.GetChannelItems(query, i => i.IsSeries && string.Equals(i.Name.GetMD5().ToString("N"), hash, StringComparison.Ordinal), cancellationToken);
            }

            if (string.Equals(query.FolderId, "kids", StringComparison.OrdinalIgnoreCase))
            {
                return this.GetChannelItems(query, i => i.IsKids, cancellationToken);
            }

            if (string.Equals(query.FolderId, "movies", StringComparison.OrdinalIgnoreCase))
            {
                return this.GetChannelItems(query, i => i.IsMovie, cancellationToken);
            }

            if (string.Equals(query.FolderId, "news", StringComparison.OrdinalIgnoreCase))
            {
                return this.GetChannelItems(query, i => i.IsNews, cancellationToken);
            }

            if (string.Equals(query.FolderId, "sports", StringComparison.OrdinalIgnoreCase))
            {
                return this.GetChannelItems(query, i => i.IsSports, cancellationToken);
            }

            if (string.Equals(query.FolderId, "others", StringComparison.OrdinalIgnoreCase))
            {
                return this.GetChannelItems(query, i => !i.IsSports && !i.IsNews && !i.IsMovie && !i.IsKids && !i.IsSeries, cancellationToken);
            }

            var result = new ChannelItemResult()
                             {
                                 Items = new List<ChannelItemInfo>()
                             };

            return Task.FromResult(result);
        }

        public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, Func<MyRecordingInfo, bool> filter, CancellationToken cancellationToken)
        {
            var service = this.GetService();
            var allRecordings = await service.GetAllRecordingsAsync(cancellationToken).ConfigureAwait(false);

            var result = new ChannelItemResult()
                             {
                                 Items = new List<ChannelItemInfo>()
                             };

            result.Items.AddRange(allRecordings.Where(filter).Select(this.ConvertToChannelItem));

            return result;
        }

        public IEnumerable<ImageType> GetSupportedChannelImages()
        {
            return new List<ImageType>
                       {
                           ImageType.Primary
                       };
        }

        public bool IsEnabledFor(string userId)
        {
            return true;
        }

        public void OnContentChanged()
        {
            if (this.ContentChanged != null)
            {
                this.ContentChanged(this, EventArgs.Empty);
            }
        }

        private static string BuildRecordingPath(string id, TvHeadendTunerConfig config, string webRoot)
        {
            try
            {
                var tvhServerName = config.TvhServerName.Trim();
                var httpPort = config.HttpPort;
                var htspPort = config.HtspPort;
                if (webRoot.EndsWith("/"))
                {
                    webRoot = webRoot.Substring(0, webRoot.Length - 1);
                }

                var userName = config.Username.Trim();
                var password = config.Password.Trim();
                return "http://" + userName + ":" + password + "@" + tvhServerName + ":" + httpPort + webRoot + "/dvrfile/" + id;
            }
            catch (Exception)
            {
            }

            return string.Empty;
        }

        private ChannelItemInfo ConvertToChannelItem(MyRecordingInfo item)
        {
            var path = BuildRecordingPath(item.Id, this.tunerConfig, this.webRoot);

            var channelItem = new ChannelItemInfo
                                  {
                                      Name = string.IsNullOrEmpty(item.EpisodeTitle) ? item.Name : item.EpisodeTitle,
                                      SeriesName = !string.IsNullOrEmpty(item.EpisodeTitle) || item.IsSeries ? item.Name : null,
                                      OfficialRating = item.OfficialRating,
                                      CommunityRating = item.CommunityRating,
                                      ContentType = item.IsMovie ? ChannelMediaContentType.Movie : item.IsSeries ? ChannelMediaContentType.Episode : ChannelMediaContentType.Clip,
                                      Genres = item.Genres,
                                      ImageUrl = item.ImageUrl,

                                      // HomePageUrl = item.HomePageUrl
                                      Id = item.Id,

                                      // IndexNumber = item.IndexNumber,
                                      MediaType = item.ChannelType == ChannelType.TV ? ChannelMediaType.Video : ChannelMediaType.Audio,
                                      MediaSources = new List<MediaSourceInfo>
                                                         {
                                                             new MediaSourceInfo
                                                                 {
                                                                     Path = path,
                                                                     Protocol = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? MediaProtocol.Http : MediaProtocol.File,
                                                                     Id = item.Id
                                                                 }
                                                         },

                                      // ParentIndexNumber = item.ParentIndexNumber,
                                      PremiereDate = item.OriginalAirDate,

                                      // ProductionYear = item.ProductionYear,
                                      // Studios = item.Studios,
                                      Type = ChannelItemType.Media,
                                      DateModified = item.DateLastUpdated,
                                      Overview = item.Overview,

                                      // People = item.People
                                      IsLiveStream = item.Status == RecordingStatus.InProgress,
                                      Etag = item.Status.ToString()
                                  };

            return channelItem;
        }

        private async Task<ChannelItemResult> GetRecordingGroups(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var service = this.GetService();

            var allRecordings = await service.GetAllRecordingsAsync(cancellationToken).ConfigureAwait(false);
            var result = new ChannelItemResult()
                             {
                                 Items = new List<ChannelItemInfo>()
                             };

            var series = allRecordings
                .Where(i => i.IsSeries)
                .ToLookup(i => i.Name, StringComparer.OrdinalIgnoreCase);

            result.Items.AddRange(
                series.OrderBy(i => i.Key).Select(
                    i => new ChannelItemInfo
                             {
                                 Name = i.Key,
                                 FolderType = ChannelFolderType.Container,
                                 Id = "series_" + i.Key.GetMD5().ToString("N"),
                                 Type = ChannelItemType.Folder,
                                 ImageUrl = i.First().ImageUrl
                             }));

            var kids = allRecordings.FirstOrDefault(i => i.IsKids);

            if (kids != null)
            {
                result.Items.Add(
                    new ChannelItemInfo
                        {
                            Name = "Kids",
                            FolderType = ChannelFolderType.Container,
                            Id = "kids",
                            Type = ChannelItemType.Folder,
                            ImageUrl = kids.ImageUrl
                        });
            }

            var movies = allRecordings.FirstOrDefault(i => i.IsMovie);
            if (movies != null)
            {
                result.Items.Add(
                    new ChannelItemInfo
                        {
                            Name = "Movies",
                            FolderType = ChannelFolderType.Container,
                            Id = "movies",
                            Type = ChannelItemType.Folder,
                            ImageUrl = movies.ImageUrl
                        });
            }

            var news = allRecordings.FirstOrDefault(i => i.IsNews);
            if (news != null)
            {
                result.Items.Add(
                    new ChannelItemInfo
                        {
                            Name = "News",
                            FolderType = ChannelFolderType.Container,
                            Id = "news",
                            Type = ChannelItemType.Folder,
                            ImageUrl = news.ImageUrl
                        });
            }

            var sports = allRecordings.FirstOrDefault(i => i.IsSports);
            if (sports != null)
            {
                result.Items.Add(
                    new ChannelItemInfo
                        {
                            Name = "Sports",
                            FolderType = ChannelFolderType.Container,
                            Id = "sports",
                            Type = ChannelItemType.Folder,
                            ImageUrl = sports.ImageUrl
                        });
            }

            var other = allRecordings.FirstOrDefault(i => !i.IsSports && !i.IsNews && !i.IsMovie && !i.IsKids && !i.IsSeries);
            if (other != null)
            {
                result.Items.Add(
                    new ChannelItemInfo
                        {
                            Name = "Others",
                            FolderType = ChannelFolderType.Container,
                            Id = "others",
                            Type = ChannelItemType.Folder,
                            ImageUrl = other.ImageUrl
                        });
            }

            return result;
        }

        private LiveTvService GetService()
        {
            return this.liveTvManager.Services.OfType<LiveTvService>().First();
        }

        private void OnUpdateTimerCallback(object state)
        {
            this.OnContentChanged();
        }
    }

    public class MyRecordingInfo
    {
        public MyRecordingInfo()
        {
            this.Genres = new List<string>();
        }

        /// <summary>
        /// Gets or sets the audio.
        /// </summary>
        /// <value>The audio.</value>
        public ProgramAudio? Audio { get; set; }

        /// <summary>
        /// ChannelId of the recording.
        /// </summary>
        public string ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the type of the channel.
        /// </summary>
        /// <value>The type of the channel.</value>
        public ChannelType ChannelType { get; set; }

        /// <summary>
        /// Gets or sets the community rating.
        /// </summary>
        /// <value>The community rating.</value>
        public float? CommunityRating { get; set; }

        /// <summary>
        /// Gets or sets the date last updated.
        /// </summary>
        /// <value>The date last updated.</value>
        public DateTimeOffset DateLastUpdated { get; set; }

        /// <summary>
        /// The end date of the recording, in UTC.
        /// </summary>
        public DateTimeOffset EndDate { get; set; }

        /// <summary>
        /// Gets or sets the episode title.
        /// </summary>
        /// <value>The episode title.</value>
        public string EpisodeTitle { get; set; }

        /// <summary>
        /// Genre of the program.
        /// </summary>
        public List<string> Genres { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance has image.
        /// </summary>
        /// <value><c>null</c> if [has image] contains no value, <c>true</c> if [has image]; otherwise, <c>false</c>.</value>
        public bool? HasImage { get; set; }

        /// <summary>
        /// Id of the recording.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Supply the image path if it can be accessed directly from the file system
        /// </summary>
        /// <value>The image path.</value>
        public string ImagePath { get; set; }

        /// <summary>
        /// Supply the image url if it can be downloaded
        /// </summary>
        /// <value>The image URL.</value>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is hd.
        /// </summary>
        /// <value><c>true</c> if this instance is hd; otherwise, <c>false</c>.</value>
        public bool? IsHd { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is kids.
        /// </summary>
        /// <value><c>true</c> if this instance is kids; otherwise, <c>false</c>.</value>
        public bool IsKids { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is live.
        /// </summary>
        /// <value><c>true</c> if this instance is live; otherwise, <c>false</c>.</value>
        public bool IsLive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is movie.
        /// </summary>
        /// <value><c>true</c> if this instance is movie; otherwise, <c>false</c>.</value>
        public bool IsMovie { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is news.
        /// </summary>
        /// <value><c>true</c> if this instance is news; otherwise, <c>false</c>.</value>
        public bool IsNews { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is premiere.
        /// </summary>
        /// <value><c>true</c> if this instance is premiere; otherwise, <c>false</c>.</value>
        public bool IsPremiere { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is repeat.
        /// </summary>
        /// <value><c>true</c> if this instance is repeat; otherwise, <c>false</c>.</value>
        public bool IsRepeat { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is series.
        /// </summary>
        /// <value><c>true</c> if this instance is series; otherwise, <c>false</c>.</value>
        public bool IsSeries { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is sports.
        /// </summary>
        /// <value><c>true</c> if this instance is sports; otherwise, <c>false</c>.</value>
        public bool IsSports { get; set; }

        /// <summary>
        /// Name of the recording.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the official rating.
        /// </summary>
        /// <value>The official rating.</value>
        public string OfficialRating { get; set; }

        /// <summary>
        /// Gets or sets the original air date.
        /// </summary>
        /// <value>The original air date.</value>
        public DateTimeOffset? OriginalAirDate { get; set; }

        /// <summary>
        /// Gets or sets the overview.
        /// </summary>
        /// <value>The overview.</value>
        public string Overview { get; set; }

        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        /// <value>The path.</value>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the program identifier.
        /// </summary>
        /// <value>The program identifier.</value>
        public string ProgramId { get; set; }

        /// <summary>
        /// Gets or sets the series timer identifier.
        /// </summary>
        /// <value>The series timer identifier.</value>
        public string SeriesTimerId { get; set; }

        /// <summary>
        /// Gets or sets the show identifier.
        /// </summary>
        /// <value>The show identifier.</value>
        public string ShowId { get; set; }

        /// <summary>
        /// The start date of the recording, in UTC.
        /// </summary>
        public DateTimeOffset StartDate { get; set; }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>The status.</value>
        public RecordingStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the timer identifier.
        /// </summary>
        /// <value>The timer identifier.</value>
        public string TimerId { get; set; }

        /// <summary>
        /// Gets or sets the URL.
        /// </summary>
        /// <value>The URL.</value>
        public string Url { get; set; }
    }
}