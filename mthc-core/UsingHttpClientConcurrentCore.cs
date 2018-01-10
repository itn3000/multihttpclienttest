using System;

namespace multithreadedhttpclient_core
{
    using System.Threading;
    using System.Net;
    using System.Net.Http;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Hosting;
    using System.Linq;
    // for using HttpClient in single thread
    class HttpClientFactory : IDisposable
    {
        Thread m_Thread;
        readonly HttpClient m_Client = new HttpClient();
        CancellationTokenSource m_Cancel = new CancellationTokenSource();
        ManualResetEventSlim m_Event = new ManualResetEventSlim(false);

        ConcurrentQueue<(Func<HttpClient, Task> f, TaskCompletionSource<int> c)> m_Queue
            = new ConcurrentQueue<(Func<HttpClient, Task> f, TaskCompletionSource<int> c)>();
        public HttpClientFactory()
        {
            StartThread();
        }
        public Task Enqueue(Func<HttpClient, Task> f)
        {
            TaskCompletionSource<int> completionSource = new TaskCompletionSource<int>();
            m_Queue.Enqueue((f, completionSource));
            m_Event.Set();
            return completionSource.Task;
        }
        void StartThread()
        {
            m_Thread = new Thread(() =>
            {
                while (!m_Cancel.IsCancellationRequested)
                {
                    while (!m_Cancel.IsCancellationRequested)
                    {
                        if (!m_Queue.TryDequeue(out var value))
                        {
                            m_Event.Reset();
                            break;
                        }
                        var task = value.f(m_Client);
                        var comp = value.c;
                        try
                        {
                            task.ContinueWith(t =>
                            {
                                if (t.IsCanceled)
                                {
                                    comp.TrySetCanceled();
                                }
                                else if (t.IsFaulted)
                                {
                                    comp.TrySetException(t.Exception);
                                }
                                else
                                {
                                    comp.TrySetResult(0);
                                }
                            }).Wait(m_Cancel.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // ignore cancel exception
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"task wait exception:{e}");
                        }
                    }
                    if (!m_Cancel.IsCancellationRequested)
                    {
                        try
                        {
                            m_Event.Wait(TimeSpan.FromSeconds(1), m_Cancel.Token);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"waiting event failed: {e}");
                        }
                    }
                }
                Console.WriteLine($"thread exited");
            });
            m_Thread.Start();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    m_Cancel.Cancel();
                    m_Event.Set();
                    Console.WriteLine($"waiting thread: {m_Thread.ThreadState},{m_Thread.ManagedThreadId},{m_Event.IsSet}");
                    m_Thread.Join();
                    Console.WriteLine($"waiting thread done: {m_Thread.ThreadState},{m_Thread.ManagedThreadId},{m_Event.IsSet}");
                    m_Event.Dispose();
                    m_Client.Dispose();
                    m_Cancel.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
    class Startup
    {
        public void Configure(IApplicationBuilder app)
        {
            app.Use(async (ctx, next) =>
            {
                await ctx.Response.WriteAsync("Hello World").ConfigureAwait(false);
            });
        }
    }
    class Program
    {
        static int LoopNum;
        static int ConcurrentNum;
        static int ConnectionLimit;
        // executing POST request using HttpClient
        static async Task DoRequest(HttpClient c, int idx, int i)
        {
            var form = new KeyValuePair<string, string>[1]
            {
                new KeyValuePair<string, string>("x", $"{idx},{i}")
            };
            var data = Enumerable.Range(0, 10).Select(x => (byte)x).ToArray();
            using (var content = new ByteArrayContent(data))
            {
                using (var res = await c.PostAsync("http://127.0.0.1:10001/MyModule/A", content).ConfigureAwait(false))
                {
                    res.EnsureSuccessStatusCode();
                }
            }
        }

        // executing post request using HttpWebRequest
        static async Task DoRequestWeb()
        {
            var req = WebRequest.CreateHttp("http://127.0.0.1:10001/MyModule/A");
            {
                req.Method = "POST";
                var data = Enumerable.Range(0, 10).Select(x => (byte)x).ToArray();
                using (var stm = req.GetRequestStream())
                {
                    await stm.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                }
                using (var res = req.GetResponse())
                {
                }
            }
        }

        static async Task MultiThreadedHttpWebRequest()
        {
            // Do 1000 concurrent tasks, loop 10 times
            var tasks = Enumerable.Range(0, ConcurrentNum).Select(async idx =>
            {
                for (int i = 0; i < LoopNum; i++)
                {
                    try
                    {
                        // using HttpWebRequest
                        await DoRequestWeb();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"error({idx},{i}): {e}");
                    }
                }
                Console.WriteLine($"done{idx}");
            }).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            Console.WriteLine($"all done");
        }

        static HttpClient CreateHttpClient()
        {
            if (ConnectionLimit <= 0)
            {
                return new HttpClient();
            }
            else
            {
                return new HttpClient(new HttpClientHandler()
                {
                    MaxConnectionsPerServer = ConnectionLimit
                });
            }
        }

        static async Task MutliThreadedHttpRequest()
        {
            using (var client = CreateHttpClient())
            {
                // Do 1000 concurrent tasks, loop 10 times
                var tasks = Enumerable.Range(0, ConcurrentNum).Select(async idx =>
                {
                    for (int i = 0; i < LoopNum; i++)
                    {
                        try
                        {
                            // using singleton HttpClient
                            await DoRequest(client, idx, i).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"error({idx},{i}): {e}");
                        }
                    }
                    Console.WriteLine($"done{idx}");
                }).ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);
                Console.WriteLine($"all done");
            }
        }
        static async Task SingleThreadRequest()
        {
            using (var factory = new HttpClientFactory())
            {
                var tasks = Enumerable.Range(0, ConcurrentNum).Select(async idx =>
                {
                    for (int i = 0; i < LoopNum; i++)
                    {
                        await factory.Enqueue(async c =>
                        {
                            await DoRequest(c, i, i).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }
                    Console.WriteLine($"task done{idx}");
                }).ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }
        static int GetIntegerFromEnv(string key, int defaultValue)
        {
            var envstr = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(envstr) && int.TryParse(envstr, out var ret))
            {
                return ret;
            }
            return defaultValue;
        }
        static void Main(string[] args)
        {
            int method = 0;
            if (args.Length > 0)
            {
                if (int.TryParse(args[0], out var tmp))
                {
                    method = tmp;
                }
            }
            ConcurrentNum = GetIntegerFromEnv("MTHC_CONCURRENT_NUM", 1000);
            LoopNum = GetIntegerFromEnv("MTHC_LOOP_NUM", 10);
            ConnectionLimit = GetIntegerFromEnv("MTHC_CONNECTION_LIMIT", -1);
            if (ConnectionLimit > 0)
            {
                ServicePointManager.DefaultConnectionLimit = ConnectionLimit;
            }
            using (var ctoken = new CancellationTokenSource())
            {
                var svrtask = Task.Run(async () =>
                {
                    using (var host = WebHost.CreateDefaultBuilder()
                        .UseStartup<Startup>()
                        .UseUrls("http://127.0.0.1:10001")
                        .Build()
                    )
                    {
                        await host.RunAsync(ctoken.Token).ConfigureAwait(false);
                    }
                });
                var clienttask = Task.Run(async () =>
                {
                    // wait for starting http server
                    await Task.Delay(1000);
                    var sw = new System.Diagnostics.Stopwatch();
                    sw.Start();
                    switch (method)
                    {
                        case 1:
                            await SingleThreadRequest().ConfigureAwait(false);
                            break;
                        case 2:
                            await MutliThreadedHttpRequest().ConfigureAwait(false);
                            break;
                        default:
                            await MultiThreadedHttpWebRequest().ConfigureAwait(false);
                            break;
                    }
                    ctoken.Cancel();
                    sw.Stop();
                    Console.WriteLine($"elapsed: {sw.Elapsed}");
                });
                Task.WhenAll(svrtask, clienttask).Wait();
            }
        }
    }
}
