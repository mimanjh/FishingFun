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

        public event EventHandler<FishingEvent> FishingEventHandler;

        public FishingBot(IBobberFinder bobberFinder, IBiteWatcher biteWatcher, ConsoleKey castKey, Dictionary<String, ConsoleKey> keys)
        {
            this.bobberFinder = bobberFinder;
            this.biteWatcher = biteWatcher;
            this.castKey = castKey;
            this.keys = keys;

            logger.Info("FishBot Created.");
            logger.Info("castKey: " + castKey);
            foreach (var key in keys.Keys)
            {
                logger.Info("macro key: " + key);
            }

            FishingEventHandler += (s, e) => { };
        }

        public void Start()
        {
            biteWatcher.FishingEventHandler = (e) => FishingEventHandler?.Invoke(this, e);

            isEnabled = true;

            if (keys.TryGetValue("60Min", out ConsoleKey sixtyMinKey))
            {
                logger.Info("Pressing 60Min key.");
                AdditionalKeyPress(sixtyMinKey);
                Sleep(random.Next(11000, 13000)); // 11-13 seconds
                lastSixtyMinPress = DateTime.Now;
            }

            // Handle 15Min key
            if (keys.TryGetValue("15Min", out ConsoleKey fifteenMinKey))
            {
                logger.Info("Pressing 15Min key.");
                AdditionalKeyPress(fifteenMinKey);
                Sleep(random.Next(1000, 1500)); // 1-1.5 seconds
                lastFifteenMinPress = DateTime.Now;
            }

            // Handle 30Sec key as usual
            if (keys.TryGetValue("30Sec", out ConsoleKey thirtySecKey))
            {
                logger.Info("Pressing 30Sec key.");
                AdditionalKeyPress(thirtySecKey);
                Sleep(random.Next(1000, 1150));
                lastThirtySecPress = DateTime.Now;
            }

            while (isEnabled)
            {
                try
                {
                    logger.Info($"Pressing key {castKey} to Cast.");

                    // Press additional keys if due
                    PressAdditionalKeysIfDue();

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

        private void PressAdditionalKeysIfDue()
        {
            // Handle 60Min key
            if (keys.TryGetValue("60Min", out ConsoleKey sixtyMinKey) &&
                (DateTime.Now - lastSixtyMinPress).TotalMinutes > random.Next(55, 59))
            {
                logger.Info("Pressing 60Min key.");
                AdditionalKeyPress(sixtyMinKey);
                Sleep(random.Next(11000, 13000)); // 11-13 seconds
                lastSixtyMinPress = DateTime.Now;
            }

            // Handle 15Min key
            if (keys.TryGetValue("15Min", out ConsoleKey fifteenMinKey) &&
                (DateTime.Now - lastFifteenMinPress).TotalMinutes > random.Next(13, 16))
            {
                logger.Info("Pressing 15Min key.");
                AdditionalKeyPress(fifteenMinKey);
                Sleep(random.Next(1000, 1500)); // 1-1.5 seconds
                lastFifteenMinPress = DateTime.Now;
            }

            // Handle 30Sec key as usual
            if (keys.TryGetValue("30Sec", out ConsoleKey thirtySecKey) &&
                (DateTime.Now - lastThirtySecPress).TotalSeconds > random.Next(25, 29))
            {
                logger.Info("Pressing 30Sec key.");
                AdditionalKeyPress(thirtySecKey);
                Sleep(random.Next(1000, 1150));
                lastThirtySecPress = DateTime.Now;
            }
        }

        private void PressAdditionalKeyIfDue(int dueSeconds, ConsoleKey key)
        {
            // Generate randomized interval
            int randomOffset = new Random().Next(-5, 5);
            double threshold = dueSeconds + randomOffset;

            if ((DateTime.Now - lastThirtySecPress).TotalSeconds > threshold)
            {
                logger.Info($"Pressing additional key: {key} after {threshold} seconds.");
                AdditionalKeyPress(key);

                // Reset lastThirtySecPress after the key press
                lastThirtySecPress = DateTime.Now;
            }
        }

        private void AdditionalKeyPress(ConsoleKey key)
        {
            lastThirtySecPress = DateTime.Now;

            FishingEventHandler?.Invoke(this, new FishingEvent { Action = FishingAction.Cast });

            logger.Info($"Pressing key {key} to run a macro.");
            WowProcess.PressKey(key);
            Sleep(1000);
        }

        private void Loot(Point bobberPosition)
        {
            logger.Info($"Pressing Key: D0 to loot.");
            var randomNumber = random.Next(1500, 2000);
            Sleep(1500);
            WowProcess.PressKey(ConsoleKey.D0);
            Sleep(1000);
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
