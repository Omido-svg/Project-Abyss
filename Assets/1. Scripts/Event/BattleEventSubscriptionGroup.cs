using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BattleEventSubscriptionGroup : IDisposable
{
    private sealed class Subscription
    {
        public Action Unsubscribe;
        public string Label;
    }

    private readonly List<Subscription> subscriptions = new();

    public int Count => subscriptions.Count;
    public bool IsDisposed { get; private set; }

    public void Subscribe(
        Action subscribe,
        Action unsubscribe,
        string label = null)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(
                nameof(BattleEventSubscriptionGroup));
        }

        if (subscribe == null)
            throw new ArgumentNullException(nameof(subscribe));

        if (unsubscribe == null)
            throw new ArgumentNullException(nameof(unsubscribe));

        bool subscribed = false;

        try
        {
            subscribe();
            subscribed = true;

            subscriptions.Add(
                new Subscription
                {
                    Unsubscribe = unsubscribe,
                    Label = string.IsNullOrWhiteSpace(label)
                        ? "UnnamedSubscription"
                        : label
                });
        }
        catch
        {
            if (subscribed)
            {
                try
                {
                    unsubscribe();
                }
                catch (Exception cleanupException)
                {
                    Debug.LogException(cleanupException);
                }
            }

            throw;
        }
    }

    public void Clear()
    {
        if (subscriptions.Count == 0)
            return;

        Subscription[] snapshot =
            subscriptions.ToArray();

        // 해제 콜백이 다시 Clear를 호출해도
        // 같은 목록을 재진입하지 않도록 먼저 비운다.
        subscriptions.Clear();

        for (int i = snapshot.Length - 1;
             i >= 0;
             i--)
        {
            Subscription subscription = snapshot[i];

            if (subscription?.Unsubscribe == null)
                continue;

            try
            {
                subscription.Unsubscribe();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[BattleEventSubscriptionGroup] " +
                    $"구독 해제 실패 / Label={subscription.Label}");

                Debug.LogException(exception);
            }
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        Clear();
        IsDisposed = true;
    }
}
