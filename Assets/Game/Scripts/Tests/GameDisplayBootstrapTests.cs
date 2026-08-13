using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GameDisplayBootstrapTests
    {
        [Test]
        public void StripsChrome_AfterAssembliesLoaded()
        {
            AssertLoadType("OnAfterAssembliesLoaded", RuntimeInitializeLoadType.AfterAssembliesLoaded);
        }

        [Test]
        public void StripsChrome_BeforeSplashScreen()
        {
            AssertLoadType("OnBeforeSplashScreen", RuntimeInitializeLoadType.BeforeSplashScreen);
        }

        static void AssertLoadType(string methodName, RuntimeInitializeLoadType expected)
        {
            var method = typeof(GameDisplayBootstrap).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.That(attribute, Is.Not.Null, methodName);
            Assert.AreEqual(expected, attribute.loadType);
        }
    }
}
