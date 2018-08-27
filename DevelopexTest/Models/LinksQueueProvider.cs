﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DevelopexTest.EventBus;
using DevelopexTest.EventBus.Events;

namespace DevelopexTest.Models
{
    public class LinksQueueProvider
    {
        private int _linkCounter = 0;

        private readonly int _maxUrlsCount;

        private object _lock = new object();

        private List<WebPageLink> _linkList = new List<WebPageLink>();

        private BlockingCollection<KeyValuePair<int, string>> _linkQueue;

        private static readonly List<SubscriptionToken> _subscriptions = new List<SubscriptionToken>();

        private class WebPageLink
        {
            public int TraverseLevel { get; private set; }
            public bool isVisited { get; private set; }
            public string Url { get; private set; }

            public WebPageLink(string url, int traverseLevel, bool isVisited = false)
            {
                Url = url;
                TraverseLevel = traverseLevel;
                isVisited = isVisited;
            }
        }

        public LinksQueueProvider(string rootLink, int maxUrlsCount)
        {
            _maxUrlsCount = maxUrlsCount;
            _subscriptions.Add(EventBus.EventBus.Instance.Subscribe<LinksFindedEvent>(OnLinkFinded));

            var priorityQueue = new SimplePriorityQueue<int, string>(maxUrlsCount);
            _linkQueue = new BlockingCollection<KeyValuePair<int, string>>(priorityQueue);

            AddLink(rootLink, 0);
            _linkCounter++;
        }

        private KeyValuePair<int, string> CreateQueueItem(WebPageLink wpLink)
        {
            return new KeyValuePair<int, string>(wpLink.TraverseLevel, wpLink.Url);
        }

        private void OnLinkFinded(LinksFindedEvent eventItem)
        {
            lock (_lock)
            {
                if (_linkCounter >= _maxUrlsCount)
                {
                    if (_linkQueue.IsCompleted)
                    {
                        return;
                    }
                    _linkQueue.CompleteAdding();
                    EventBus.EventBus.Instance.Unsubscribe(_subscriptions.Find(x =>
                        x.EventItemType == eventItem.GetType()));
                    return;
                }
                var delta = _maxUrlsCount - _linkCounter;
                var parentTraverseLevel = _linkList.Find(x => x.Url == eventItem.ParentLink).TraverseLevel;
                WebPageLink parentLink = new WebPageLink(eventItem.ParentLink, parentTraverseLevel);
                foreach (var innerLink in eventItem.InnerLinks.Take(delta))
                {
                    if (!_linkList.Any(x => x.Url == innerLink))
                    {
                        AddLink(innerLink, parentLink.TraverseLevel + 1);
                        _linkCounter++;
                    }
                }
            }
        }

        private void AddLink(string link, int traverseLevel)
        {
            var webPageLink = new WebPageLink(link, traverseLevel);
            _linkList.Add(webPageLink);
            _linkQueue.Add(CreateQueueItem(webPageLink));
        }

        public BlockingCollection<KeyValuePair<int, string>> LinkQueue
        {
            get { return _linkQueue; }

            set { }
        }
    }
}