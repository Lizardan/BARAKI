using System;
using Game.UI.Bindings;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Game.Tests
{
    public class UIToolkitRxExtensionsTests
    {
        [Test]
        public void SubscribeToText_UpdatesLabel()
        {
            var label = new Label();
            var property = new ReactiveProperty<string>("Hello");

            using var subscription = property.SubscribeToText(label);

            Assert.AreEqual("Hello", label.text);

            property.Value = "World";
            Assert.AreEqual("World", label.text);

            subscription.Dispose();
            property.Dispose();
        }

        [Test]
        public void UIBindingScope_Dispose_CleansSubscriptions()
        {
            var root = new VisualElement();
            var scope = new UIBindingScope(root);
            var disposed = false;
            scope.Add(Disposable.Create(() => disposed = true));

            scope.Dispose();

            Assert.IsTrue(disposed);
        }
    }
}
