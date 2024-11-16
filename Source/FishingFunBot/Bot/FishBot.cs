using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace FishingFun
{
    public class FishingBot
    {
        public static ILog logger = LogManager.GetLogger("Fishbot");

        private ConsoleKey castKey;
        private Dictionary<String, ConsoleKey> keys;
        private IBobberFinder bobberFinder;
        private IBiteWatcher biteWatcher;
        private bool isEnabled;
        private Stopwatch stopwatch = new Stopwatch();
        private static Random random = new Random();

        private DateTime lastSixtyMinPress = DateTime.MinValue;
        private DateTime lastFifteenMinPress = DateTime.MinValue;
        private DateTime lastThirtySecPress = DateTime.MinValue;
        private DateTime lastRestTime = DateTime.Now;

        public event EventHandler<FishingEvent> FishingEventHandler;

        public FishingBot(IBobberFinder bobberFinder, IBiteWatcher biteWatcher, ConsoleKey castKey, Dictionary<String, ConsoleKey> keys)
        {
            this.bobberFinder = bobberFinder;
            this.biteWatcher = biteWatcher;
            this.castKey = castKey;
            this.keys = keys;

            logger.Info("FishBot Created.");
            logger.Info("castKey: " + castKey);
            foreach (var key in keys)
            {
                logger.Info("macro key " + key.Key + ": " + key.Value);
            }

            FishingEventHandler += (s, e) => { };
        }

        public void Start()
        {
            biteWatcher.FishingEventHandler = (e) => FishingEventHandler?.Invoke(this, e);

            isEnabled = true;

            while (isEnabled)
            {
                try
                {
                    // Rest every 30-90 minutes for 2-5 minutes
                    restAtRandomTimes();

                    // Press additional keys if due
                    PressAdditionalKeysIfDue();

                    logger.Info($"Pressing key {castKey} to fish.");

                    FishingEventHandler?.Invoke(this, new FishingEvent { Action = FishingAction.Cast });
                    WowProcess.PressKey(castKey);

                    Watch(2000);

                    WaitForBite();
                }
                catch (Exception e)
                {
                    logger.Error(e.ToString());
                    Sleep(2000);
                }
            }

            logger.Error("Bot has Stopped.");
        }

        public void SetCastKey(ConsoleKey castKey)
        {
            this.castKey = castKey;
        }

        private void Watch(int milliseconds)
        {
            bobberFinder.Reset();
            stopwatch.Reset();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < milliseconds)
            {
                bobberFinder.Find();
            }
            stopwatch.Stop();
        }

        public void Stop()
        {
            isEnabled = false;
            logger.Error("Bot is Stopping...");
        }

        private void WaitForBite()
        {
            bobberFinder.Reset();
            var bobberPosition = FindBobber();

            if (bobberPosition == Point.Empty)
            {
                logger.Info("Failed to find bobber.");
                return;
            }

            biteWatcher.Reset(bobberPosition);
            logger.Info("Bobber start position: " + bobberPosition);

            var timedTask = new TimedAction(
                (a) => logger.Info("Fishing timed out!"),
                25 * 1000, // 25 seconds timeout
                25
            );

            while (isEnabled)
            {
                var currentBobberPosition = FindBobber();

                if (currentBobberPosition == Point.Empty || currentBobberPosition.X == 0)
                {
                    logger.Info("Bobber lost, exiting wait.");
                    return;
                }

                if (biteWatcher.IsBite(currentBobberPosition))
                {
                    Loot(bobberPosition);

                    return;
                }

                if (!timedTask.ExecuteIfDue())
                {
                    logger.Info("Timed task completed, exiting.");
                    return;
                }
            }
        }

        private void restAtRandomTimes()
        {
            // Generate random intervals for when to rest (30 to 90 minutes)
            int minRestInterval = 30 * 60 * 1000; 
            int maxRestInterval = 90 * 60 * 1000; 
            int restDurationMin = 2 * 60 * 1000; 
            int restDurationMax = 5 * 60 * 1000;  

            // Check if the time since the last rest exceeds the random interval
            if ((DateTime.Now - lastRestTime).TotalMilliseconds > random.Next(minRestInterval, maxRestInterval))
            {
                int restDuration = random.Next(restDurationMin, restDurationMax);
                logger.Info($"Resting for {restDuration / 1000} seconds.");
                Sleep(restDuration);

                lastRestTime = DateTime.Now; // Update the last rest time
            }
        }

        private void PressAdditionalKeysIfDue()
        {
            // Handle 60MinShort key
            if (keys.TryGetValue("60MinShort", out ConsoleKey sixtyMinKeyShort) &&
                (DateTime.Now - lastSixtyMinPress).TotalMinutes > random.Next(55, 59))
            {
                logger.Info("Pressing 60MinShort key twice.");
                for (int i = 0; i < 2; i++)
                {
                    AdditionalKeyPress(sixtyMinKeyShort);
                    Sleep(random.Next(200, 250)); // total of 1-1.25 seconds
                }

                Sleep(random.Next(1000, 1100));

                keys.TryGetValue("60MinLong", out ConsoleKey sixtyMinLongKey);
                logger.Info("Pressing 60MinLong key.");
                AdditionalKeyPress(sixtyMinLongKey);
                lastSixtyMinPress = DateTime.Now;
                Sleep(random.Next(11000, 13000)); // 11-13 seconds
            }

            // Handle 15Min key
            if (keys.TryGetValue("15Min", out ConsoleKey fifteenMinKey) &&
                (DateTime.Now - lastFifteenMinPress).TotalMinutes > random.Next(13, 16))
            {
                logger.Info("Pressing 15Min key.");
                AdditionalKeyPress(fifteenMinKey);
                lastFifteenMinPress = DateTime.Now;
                Sleep(random.Next(1000, 1500)); // 1-1.5 seconds
            }


            // Handle 30Sec key as usual
            if (keys.TryGetValue("30Sec", out ConsoleKey thirtySecKey) &&
                (DateTime.Now - lastThirtySecPress).TotalSeconds > random.Next(25, 29))
            {
                logger.Info("Pressing 30Sec key.");
                AdditionalKeyPress(thirtySecKey);
                lastThirtySecPress = DateTime.Now;
                Sleep(random.Next(1000, 1150));
            }

        }

        private void AdditionalKeyPress(ConsoleKey key)
        {
            FishingEventHandler?.Invoke(this, new FishingEvent { Action = FishingAction.Cast });

            WowProcess.PressKey(key);
        }

        private void Loot(Point bobberPosition)
        {
            logger.Info($"Pressing Key: D0 to loot.");
            Sleep(random.Next(1400, 1500));
            WowProcess.PressKey(ConsoleKey.D0);
            Sleep(random.Next(1000, 1150));
        }

        public static void Sleep(int ms)
        {
            ms += random.Next(0, 225);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (sw.Elapsed.TotalMilliseconds < ms)
            {
                FlushBuffers();
                Thread.Sleep(100);
            }
        }

        public static void FlushBuffers()
        {
            ILog log = LogManager.GetLogger("Fishbot");
            var logger = log.Logger as Logger;
            if (logger != null)
            {
                foreach (IAppender appender in logger.Appenders)
                {
                    var buffered = appender as BufferingAppenderSkeleton;
                    if (buffered != null)
                    {
                        buffered.Flush();
                    }
                }
            }
        }

        private Point FindBobber()
        {
            var timer = new TimedAction((a) => { logger.Info("Waited seconds for target: " + a.ElapsedSecs); }, 1000, 5);

            while (true)
            {
                var target = this.bobberFinder.Find();
                if (target != Point.Empty || !timer.ExecuteIfDue()) { return target; }
            }
        }
    }
}
