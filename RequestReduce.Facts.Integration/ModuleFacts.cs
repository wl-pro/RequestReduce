﻿using System;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using RequestReduce.Configuration;
using Xunit;
using TimeoutException = Xunit.Sdk.TimeoutException;

namespace RequestReduce.Facts.Integration
{
    public class ModuleFacts : IDisposable
    {
        private string rrFolder;

        public ModuleFacts()
        {
            rrFolder = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName + "\\RequestReduce.SampleWeb\\RRContent";
            if (Directory.Exists(rrFolder))
            {
                RecyclePool();
                if (Directory.Exists(rrFolder))
                    Directory.Delete(rrFolder, true);
                while (Directory.Exists(rrFolder))
                    Thread.Sleep(0);
            }
        }

        [Fact]
        public void WillReduceToOneCss()
        {
            var cssPattern = new Regex(@"<link[^>]+type=""?text/css""?[^>]+>", RegexOptions.IgnoreCase);
            new WebClient().DownloadString("http://localhost:8877/Local.html");
            WaitToCreateCss();

            var response = new WebClient().DownloadString("http://localhost:8877/Local.html");

            Assert.Equal(1, cssPattern.Matches(response).Count);
        }

        [Fact]
        public void WillUseSameReductionAfterAppPoolRecycle()
        {
            var cssPattern = new Regex(@"<link[^>]+type=""?text/css""?[^>]+>", RegexOptions.IgnoreCase);
            var urlPattern = new Regex(@"href=""?(?<url>[^"" ]+)""?[^ />]+[ />]", RegexOptions.IgnoreCase);
            new WebClient().DownloadString("http://localhost:8877/Local.html");
            WaitToCreateCss();
            var response = new WebClient().DownloadString("http://localhost:8877/Local.html");
            var css = cssPattern.Match(response).ToString();
            var url = urlPattern.Match(css).Groups["url"].Value;
            var file = url.Replace("/RRContent", rrFolder).Replace("/", "\\");
            var createTime = new FileInfo(file).LastWriteTime;

            RecyclePool();
            new WebClient().DownloadString("http://localhost:8877/Local.html");
            WaitToCreateCss();

            Assert.Equal(createTime, new FileInfo(file).LastWriteTime);
        }

        [Fact]
        public void WillSetCacheHeadersOnContent()
        {
            var cssPattern = new Regex(@"<link[^>]+type=""?text/css""?[^>]+>", RegexOptions.IgnoreCase);
            var urlPattern = new Regex(@"href=""?(?<url>[^"" ]+)""?[^ />]+[ />]", RegexOptions.IgnoreCase);
            string url;
            using (var client = new WebClient())
            {
                client.DownloadString("http://localhost:8877/Local.html");
                WaitToCreateCss();
                var response = client.DownloadString("http://localhost:8877/Local.html");
                var css = cssPattern.Match(response).ToString();
                url = urlPattern.Match(css).Groups["url"].Value;
            }

            var req = HttpWebRequest.Create("http://localhost:8877" + url);
            var response2 = req.GetResponse();

            Assert.Equal("public", response2.Headers["Cache-Control"].ToLower());
            Assert.Null(response2.Headers["ETag"]);
            response2.Close();
        }

        [Fact]
        public void WillReReduceCssAfterFileDeletion()
        {
            var cssPattern = new Regex(@"<link[^>]+type=""?text/css""?[^>]+>", RegexOptions.IgnoreCase);
            var urlPattern = new Regex(@"href=""?(?<url>[^"" ]+)""?[^ />]+[ />]", RegexOptions.IgnoreCase);
            new WebClient().DownloadString("http://localhost:8877/Local.html");
            WaitToCreateCss();
            var response = new WebClient().DownloadString("http://localhost:8877/Local.html");
            var css = cssPattern.Match(response).ToString();
            var url = urlPattern.Match(css).Groups["url"].Value;
            var file = url.Replace("/RRContent", rrFolder).Replace("/", "\\");
            var createTime = new FileInfo(file).LastWriteTime;

            File.Delete(file);
            while (File.Exists(file))
                Thread.Sleep(0);
            new WebClient().DownloadString("http://localhost:8877/Local.html");
            WaitToCreateCss();
            new WebClient().DownloadString("http://localhost:8877/Local.html");

            Assert.True(createTime < new FileInfo(file).LastWriteTime);
        }

        private void WaitToCreateCss()
        {
            var watch = new Stopwatch();
            watch.Start();
            while (!Directory.Exists(rrFolder) && watch.ElapsedMilliseconds < 5000)
                Thread.Sleep(0);
            while (Directory.GetDirectories(rrFolder).Length == 0 && watch.ElapsedMilliseconds < 5000)
                Thread.Sleep(0);
            var newDir = Directory.GetDirectories(rrFolder)[0];
            while (Directory.GetFiles(newDir, "*.css").Length == 0 && watch.ElapsedMilliseconds < 5000)
                Thread.Sleep(0);
            if(watch.ElapsedMilliseconds >= 5000)
                throw new TimeoutException(5000);
            Thread.Sleep(100);
        }

        private void RecyclePool()
        {
            var pool = new DirectoryEntry("IIS://localhost/W3SVC/AppPools/RequestReduce");
            pool.Invoke("Recycle", null);
            Thread.Sleep(1000);
        }

        public void Dispose()
        {
            if (Directory.Exists(rrFolder))
            {
                try
                {
                    RecyclePool();
                    Directory.Delete(rrFolder, true);
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                    Directory.Delete(rrFolder, true);
                }
            }
        }
    }
}
