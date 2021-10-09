﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class DownloadManager : IDownloadManager
    {
        private Dictionary<string, IDownloadRequest> _requests;
        private int _numberOfDownloads;
        public int NumberOfDownloadsInProgress => _numberOfDownloads;
        public int MaxConcurrentDownloadsDegree { get; set; }
        public DownloadConfiguration Configuration { get; }
        public event EventHandler<IDownloadRequest> AddNewDownload;
        public event EventHandler<IDownloadRequest> DownloadStarted;
        public event EventHandler<IDownloadRequest> DownloadCompleted;
        public event EventHandler<IDownloadRequest> DownloadProgressChanged;

        public DownloadManager(DownloadConfiguration configuration, int maxNumberOfMultipleFileDownload)
        {
            if (maxNumberOfMultipleFileDownload <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfMultipleFileDownload),
                    maxNumberOfMultipleFileDownload, "The value must be greater than zero.");
            }

            _requests = new Dictionary<string, IDownloadRequest>();
            Configuration = configuration;
            MaxConcurrentDownloadsDegree = maxNumberOfMultipleFileDownload;
        }

        public List<IDownloadRequest> GetDownloadRequests()
        {
            return _requests.Values.ToList();
        }

        public void DownloadAsync(string url, string path)
        {
            DownloadTaskAsync(url, path).ConfigureAwait(false);
        }
        public Task DownloadTaskAsync(string url, string path)
        {
            return DownloadTaskAsync(new DownloadRequest() { Url = url, Path = path });
        }
        public void DownloadAsync(params IDownloadRequest[] downloadRequests)
        {
            DownloadTaskAsync(downloadRequests).ConfigureAwait(false);
        }        
        public Task DownloadTaskAsync(params IDownloadRequest[] downloadRequests)
        {
            var tasks = downloadRequests.Select(req => Download(req)).ToArray();
            return Task.WhenAll(tasks);
        }

        private Task Download(IDownloadRequest downloadRequest)
        {
            if (string.IsNullOrWhiteSpace(downloadRequest?.Url))
                throw new ArgumentNullException(nameof(downloadRequest.Url));

            if (string.IsNullOrWhiteSpace(downloadRequest?.Path))
                throw new ArgumentNullException(nameof(downloadRequest.Path));

            var request = GetOrAddDownloadService(downloadRequest);
            EnqueueDownload();

            return Task.Delay(1);
        }

        private IDownloadRequest GetOrAddDownloadService(IDownloadRequest downloadRequest)
        {
            if (_requests.TryGetValue(downloadRequest.Url, out var request))
                return request;

            return CreateDownloadRequest(downloadRequest);
        }
        private IDownloadRequest CreateDownloadRequest(IDownloadRequest request)
        {
            request.DownloadService ??= new DownloadService(Configuration);
            request.DownloadService.DownloadFileCompleted +=  (s, e) => OnDownloadFileComplete(request, e);
            request.DownloadService.DownloadStarted += (s, e) => OnDownloadStarted(request);
            request.DownloadService.DownloadProgressChanged += (s, e) => OnDownloadProgressChanged(request);
            _requests.Add(request.Url, request);
            OnAddNewDownload(request);
            return request;
        }
        private void RemoveRequest(IDownloadRequest downloadRequest)
        {
            lock (this)
            {
                Interlocked.Decrement(ref _numberOfDownloads);
                Debug.Assert(_numberOfDownloads >= 0, "This algorithm isn't thread-safe! What do you do?");
                downloadRequest.IsSaving = false;
            }
        }

        private void OnDownloadFileComplete(IDownloadRequest request, AsyncCompletedEventArgs e)
        {
            RemoveRequest(request);
            request.IsSaving = false;
            request.IsSaveComplete = false;
            
            if (e.Cancelled == false)
            {
                if (e.Error == null) // download completed
                {
                    request.IsSaveComplete = true;
                    OnDownloadCompleted(request);
                }
                else
                {
                    // throw exp
                }
            }
            EnqueueDownload();
        }
        private void EnqueueDownload()
        {
            lock (this)
            {
                if (_numberOfDownloads < MaxConcurrentDownloadsDegree)
                {
                    var request = EnqueueRequest();
                    if (request != null)
                    {
                        Interlocked.Increment(ref _numberOfDownloads);
                        Debug.Assert(_numberOfDownloads <= MaxConcurrentDownloadsDegree, "This algorithm isn't thread-safe! What do you do?");
                        request.DownloadService.DownloadFileTaskAsync(request.Url, request.Path).ConfigureAwait(false);
                    }
                }
            }
        }

        private IDownloadRequest EnqueueRequest()
        {
            return _requests.Values.FirstOrDefault(req =>
                req.IsSaving && req.IsSaveComplete == false);
        }

        public void CancelAsync(string url)
        {
            throw new NotImplementedException();
        }

        public void CancelAsync(IDownloadRequest downloadInfo)
        {
            throw new NotImplementedException();
        }

        public void CancelAllAsync()
        {
            //
        }

        public void Clear()
        {
            CancelAllAsync();
            _requests.Clear();
        }

        private void OnAddNewDownload(IDownloadRequest request)
        {
            AddNewDownload?.Invoke(this, request);
        }
        private void OnDownloadStarted(IDownloadRequest request)
        {
            DownloadStarted?.Invoke(this, request);
        }
        private void OnDownloadProgressChanged(IDownloadRequest request)
        {
            DownloadProgressChanged?.Invoke(this, request);
        }
        private void OnDownloadCompleted(IDownloadRequest request)
        {
            DownloadCompleted?.Invoke(this, request);
        }

        public void Dispose()
        {
            Clear();
        }
    }
}