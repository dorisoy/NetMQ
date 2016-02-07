﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;
using NUnit.Framework;
// ReSharper disable ConvertClosureToMethodGroup

namespace NetMQ.ReactiveExtensions.Tests
{
	[TestFixture]	
	public class UnitTest
	{
		[Test]
		public void Simplest_Test()
		{
			Console.WriteLine(TestContext.CurrentContext.Test.Name);

			CountdownEvent cd = new CountdownEvent(5);
			{
				int freePort = TcpPortFree();

				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);
				pubSub.Subscribe(o =>
					{
						Console.Write($"Test 1: {o}\n");
						cd.Signal();
					},
					ex =>
					{
						Console.WriteLine($"Exception! {ex.Message}");
					});

				pubSub.OnNext(38);
				pubSub.OnNext(39);
				pubSub.OnNext(40);
				pubSub.OnNext(41);
				pubSub.OnNext(42);
			}

			if (cd.Wait(TimeSpan.FromSeconds(10)) == false) // Blocks until _countdown.Signal has been called.
			{
				Assert.Fail("Timed out, this test should complete in 10 seconds.");
			}
		}

		[Test]
		public void Simplest_Fanout_Sub()
		{
			CountdownEvent cd = new CountdownEvent(3);
			{
				int freePort = TcpPortFree();
				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);
				pubSub.Subscribe(o =>
				{
					Assert.AreEqual(o, 42);
					Console.Write($"PubTwoThreadFanoutSub1: {o}\n");
					cd.Signal();
				});
				pubSub.Subscribe(o =>
				{
					Assert.AreEqual(o, 42);
					Console.Write($"PubTwoThreadFanoutSub2: {o}\n");
					cd.Signal();
				});
				pubSub.Subscribe(o =>
				{
					Assert.AreEqual(o, 42);
					Console.Write($"PubTwoThreadFanoutSub3: {o}\n");
					cd.Signal();
				});

				pubSub.OnNext(42);
			}

			if (cd.Wait(TimeSpan.FromSeconds(10)) == false) // Blocks until _countdown.Signal has been called.
			{
				Assert.Fail("Timed out, this test should complete in 10 seconds.");
			}
		}

		[Test]
		public void OnException_Should_Get_Passed_To_Subscribers()
		{
			CountdownEvent weAreDone = new CountdownEvent(1);
			{
				int freePort = TcpPortFree();
				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);
				pubSub.Subscribe(
					o =>
					{
						// If this gets called more than max times, it will throw an exception as it is going through 0.
						Assert.Fail();
					},
					ex =>
					{
						Console.Write($"Exception: {ex.Message}");
						Assert.True(ex.Message.Contains("passed"));
						weAreDone.Signal();
					},
					() =>
					{
						Assert.Fail();
					});

				pubSub.OnError(new Exception("passed"));
			}
			if (weAreDone.Wait(TimeSpan.FromSeconds(10)) == false) // Blocks until _countdown.Signal has been called.
			{
				Assert.Fail("Timed out, this test should complete in 10 seconds.");
			}
		}

		[Test]
		public void PubSubShouldNotCrashIfNoThreadSleep()
		{
			using (var pub = new PublisherSocket())
			{
				using (var sub = new SubscriberSocket())
				{
					int freePort = TcpPortFree();
					pub.Bind("tcp://127.0.0.1:" + freePort);
					sub.Connect("tcp://127.0.0.1:" + freePort);

					sub.Subscribe("*");

					Stopwatch sw = Stopwatch.StartNew();
					{
						for (int i = 0; i < 50; i++)
						{
							pub.SendFrame("*"); // Ping.

							Console.Write("*");
							string topic;
							var gotTopic = sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out topic);
							string ping;
							var gotPing = sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out ping);
							if (gotTopic == true)
							{
								Console.Write("\n");
								break;
							}
						}
					}
					Console.WriteLine($"Connected in {sw.ElapsedMilliseconds} ms.");
				}
			}
		}

		/// <summary>
		/// Intent: Returns next free TCP/IP port. See
		/// http://stackoverflow.com/questions/138043/find-the-next-tcp-port-in-net
		/// </summary>
		/// <threadSafe>Yes. Quote: "I successfully used this technique to get a free port. I too was concerned about
		/// race-conditions, with some other process sneaking in and grabbing the recently-detected-as-free port. So I
		/// wrote a test with a forced Sleep(100) between var port = FreeTcpPort() and starting an HttpListener on the
		/// free port. I then ran 8 identical processes hammering on this in a loop. I could never hit the race
		/// condition. My anecdotal evidence (Win 7) is that the OS apparently cycles through the range of ephemeral
		/// ports (a few thousand) before coming around again. So the above snippet should be just fine." </threadSafe>
		/// <returns>A free TCP/IP port.</returns>
		public static int TcpPortFree()
		{
			TcpListener l = new TcpListener(IPAddress.Loopback, 0);
			l.Start();
			int port = ((IPEndPoint)l.LocalEndpoint).Port;
			l.Stop();
			return port;
		}

	}
}