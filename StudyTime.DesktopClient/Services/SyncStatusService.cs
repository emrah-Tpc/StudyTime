using System;

namespace StudyTime.DesktopClient.Services
{
    public class SyncStatusService
    {
        public bool IsSyncing { get; private set; }
        public bool HasPendingItems { get; private set; }
        public string? SyncError { get; private set; }

        public event Action? OnChange;

        public void SetSyncing(bool isSyncing)
        {
            if (IsSyncing != isSyncing)
            {
                IsSyncing = isSyncing;
                NotifyStateChanged();
            }
        }

        public void SetPendingItems(bool hasPending)
        {
            if (HasPendingItems != hasPending)
            {
                HasPendingItems = hasPending;
                NotifyStateChanged();
            }
        }

        public void SetError(string? error)
        {
            if (SyncError != error)
            {
                SyncError = error;
                NotifyStateChanged();
            }
        }

        public void Reset()
        {
            IsSyncing = false;
            HasPendingItems = false;
            SyncError = null;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
