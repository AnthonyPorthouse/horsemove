using System;
using System.Collections.Generic;
using Harmony;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;

namespace HorseMove
{
    public class HorsePatches
    {
        private static IMonitor _monitor;
        private static double _moveChance;
        private static int _moveMin;
        private static int _moveMax;

        private static bool _isWandering = false;
        private static int _ticksToMove = 0;
        private static Direction _moveDirection = Direction.Up;
        private static bool _moveOffFarm;
        private static bool _moveIfRaining;

        public static void Initialize(HarmonyInstance harmony, IMonitor monitor, ModConfig config)
        {
            monitor.Log($"Setting up Patches for Horse", LogLevel.Debug);

            harmony.Patch(
                original: AccessTools.Method(typeof(Horse), nameof(Horse.update),
                    new[] {typeof(GameTime), typeof(GameLocation)}),
                prefix: new HarmonyMethod(typeof(HorsePatches), nameof(Before_Update))
            );

            _monitor = monitor;
            _moveChance = config.GetWanderFrequency();
            _moveMin = config.GetWanderRange().Item1;
            _moveMax = config.GetWanderRange().Item2;
            _moveOffFarm = config.wanderOutsideOfFarm;
            _moveIfRaining = config.wanderIfRaining;
        }

        public static bool Before_Update(Horse __instance, GameTime time)
        {
            try
            {
                if (__instance == null)
                {
                    return true;
                }

                if (!__instance.currentLocation.isFarm && !_moveOffFarm)
                {
                    return true;
                }

                if (Game1.isRaining && !_moveIfRaining)
                {
                    return true;
                }

                if (__instance.rider != null)
                {
                    _isWandering = false;
                    _ticksToMove = 0;
                    return true;
                }

                return Move(__instance, time);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Error in {nameof(Before_Update)}\n{ex}", LogLevel.Error);
                return true;
            }
        }

        private static bool Move(Horse horse, GameTime time)
        {
            if (Game1.random.NextDouble() < _moveChance && !_isWandering)
            {
                _moveDirection = (Direction) Game1.random.Next(0, 4);
                _ticksToMove = Game1.random.Next(_moveMin, _moveMax);

                horse.faceDirection((int) _moveDirection);

                AnimatedSprite.endOfAnimationBehavior frameBehavior = x =>
                {
                    switch (horse.currentLocation.doesTileHaveProperty((int) horse.getTileLocation().X,
                        (int) horse.getTileLocation().Y, "Type", "Back"))
                    {
                        case "Stone":
                            horse.currentLocation.localSoundAt("stoneStep", horse.getTileLocation());
                            break;
                        case "Wood":
                            horse.currentLocation.localSoundAt("woodyStep", horse.getTileLocation());
                            break;
                        default:
                            horse.currentLocation.localSoundAt("thudStep", horse.getTileLocation());
                            break;
                    }
                };

                if (horse.FacingDirection == 1)
                    horse.Sprite.setCurrentAnimation(new List<FarmerSprite.AnimationFrame>()
                    {
                        new FarmerSprite.AnimationFrame(8, 70),
                        new FarmerSprite.AnimationFrame(9, 70, false, false, frameBehavior),
                        new FarmerSprite.AnimationFrame(10, 70, false, false, frameBehavior),
                        new FarmerSprite.AnimationFrame(11, 70, false, false, frameBehavior),
                        new FarmerSprite.AnimationFrame(12, 70),
                        new FarmerSprite.AnimationFrame(13, 70)
                    });
                else if (horse.FacingDirection == 3)
                    horse.Sprite.setCurrentAnimation(new List<FarmerSprite.AnimationFrame>()
                    {
                        new FarmerSprite.AnimationFrame(8, 70, false, true),
                        new FarmerSprite.AnimationFrame(9, 70, false, true, frameBehavior),
                        new FarmerSprite.AnimationFrame(10, 70, false, true, frameBehavior),
                        new FarmerSprite.AnimationFrame(11, 70, false, true, frameBehavior),
                        new FarmerSprite.AnimationFrame(12, 70, false, true),
                        new FarmerSprite.AnimationFrame(13, 70, false, true)
                    });
                else if (horse.FacingDirection == 0)
                    horse.Sprite.setCurrentAnimation(new List<FarmerSprite.AnimationFrame>()
                    {
                        new FarmerSprite.AnimationFrame(15, 70),
                        new FarmerSprite.AnimationFrame(16, 70, false, false, frameBehavior),
                        new FarmerSprite.AnimationFrame(17, 70, false, false, frameBehavior),
                        new FarmerSprite.AnimationFrame(18, 70, false, false, frameBehavior),
                        new FarmerSprite.AnimationFrame(19, 70),
                        new FarmerSprite.AnimationFrame(20, 70)
                    });
                else if (horse.FacingDirection == 2)
                    horse.Sprite.setCurrentAnimation(new List<FarmerSprite.AnimationFrame>()
                    {
                        new FarmerSprite.AnimationFrame(1, 70),
                        new FarmerSprite.AnimationFrame(2, 70, false, false, frameBehavior),
                        new FarmerSprite.AnimationFrame(3, 70, false, false, frameBehavior),
                        new FarmerSprite.AnimationFrame(4, 70, false, false, frameBehavior),
                        new FarmerSprite.AnimationFrame(5, 70),
                        new FarmerSprite.AnimationFrame(6, 70)
                    });
                
                _monitor.Log($"Moving {horse.getName()} {_moveDirection} for {_ticksToMove}", LogLevel.Debug);
                _isWandering = true;
            }

            if (_ticksToMove >= 0 && _isWandering)
            {
                if (horse.currentLocation.isCollidingPosition(
                    horse.nextPosition(horse.getDirection()),
                    Game1.viewport,
                    false,
                    0,
                    false,
                    horse
                ))
                {
                    _monitor.Log("Bonk! Stopping wandering.", LogLevel.Debug);
                    _isWandering = false;
                    horse.Sprite.StopAnimation();
                    horse.faceDirection((int) _moveDirection);
                    horse.Halt();
                    _ticksToMove = 0;
                    return true;
                }

                switch (_moveDirection)
                {
                    case Direction.Up:
                        horse.SetMovingOnlyUp();
                        break;
                    case Direction.Right:
                        horse.SetMovingOnlyRight();
                        break;
                    case Direction.Down:
                        horse.SetMovingOnlyDown();
                        break;
                    case Direction.Left:
                        horse.SetMovingOnlyLeft();
                        break;
                }

                horse.speed = 1;
                horse.Sprite.loop = true;
                horse.Sprite.animateOnce(time);
                horse.tryToMoveInDirection(horse.getDirection(), false, 0, false);

                _ticksToMove--;

                if (_ticksToMove == 0)
                {
                    _isWandering = false;
                    horse.Sprite.StopAnimation();
                    horse.Halt();

                    return true;
                }

                return false;
            }

            return true;
        }
    }


    public enum Direction
    {
        Up = 0,
        Right,
        Down,
        Left
    }
}