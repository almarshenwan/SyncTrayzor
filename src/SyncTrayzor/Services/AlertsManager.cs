﻿using SyncTrayzor.Services;
using SyncTrayzor.Services.Conflicts;
using SyncTrayzor.SyncThing;
using SyncTrayzor.SyncThing.ApiClient;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyncTrayzor.SyncThing.TransferHistory;

namespace SyncTrayzor.Services
{
    public interface IAlertsManager : IDisposable
    {
        event EventHandler AlertsStateChanged;
        bool AnyAlerts { get; }

        bool EnableFailedTransferAlerts { get; set; }
        bool EnableConflictedFileAlerts { get; set; }

        IReadOnlyList<string> ConflictedFiles { get; }

        IReadOnlyList<string> FoldersWithFailedTransferFiles { get; }
    }

    public class AlertsManager : IAlertsManager
    {
        private readonly ISyncThingManager syncThingManager;
        private readonly IConflictFileWatcher conflictFileWatcher;
        private readonly SynchronizedEventDispatcher eventDispatcher;

        public bool AnyAlerts => this.ConflictedFiles.Count > 0 || this.FoldersWithFailedTransferFiles.Count > 0;


        private readonly List<string> _conflictedFiles = new List<string>();
        public IReadOnlyList<string> ConflictedFiles { get; private set; }

        private readonly List<string> _foldersWithFailedTransferFiles = new List<string>();
        public IReadOnlyList<string> FoldersWithFailedTransferFiles { get; private set; }

        public event EventHandler AlertsStateChanged;

        private bool _enableFailedTransferAlerts;
        public bool EnableFailedTransferAlerts
        {
            get { return this._enableFailedTransferAlerts; }
            set
            {
                if (this._enableFailedTransferAlerts == value)
                    return;
                this._enableFailedTransferAlerts = value;
                this.ResetOutputs();
            }
        }

        private bool _enableConflictedFileAlerts;
        public bool EnableConflictedFileAlerts
        {
            get { return this._enableConflictedFileAlerts; }
            set
            {
                if (this._enableConflictedFileAlerts == value)
                    return;
                this._enableConflictedFileAlerts = value;
                this.ResetOutputs();
            }
        }

        public AlertsManager(ISyncThingManager syncThingManager, IConflictFileWatcher conflictFileWatcher)
        {
            this.syncThingManager = syncThingManager;
            this.conflictFileWatcher = conflictFileWatcher;
            this.eventDispatcher = new SynchronizedEventDispatcher(this);

            this.ResetOutputs();

            this.syncThingManager.TransferHistory.TransferCompleted += this.TransferCompleted;

            this.conflictFileWatcher.ConflictedFilesChanged += this.ConflictFilesChanged;
        }

        private void ResetOutputs()
        {
            this.FoldersWithFailedTransferFiles = this.EnableFailedTransferAlerts ? this._foldersWithFailedTransferFiles.AsReadOnly() : EmptyReadOnlyList<string>.Instance;
            this.ConflictedFiles = this.EnableConflictedFileAlerts ? this._conflictedFiles.AsReadOnly() : EmptyReadOnlyList<string>.Instance;

            this.eventDispatcher.Raise(this.AlertsStateChanged);
        }

        private void TransferCompleted(object sender, FileTransferChangedEventArgs e)
        {
            var oldFoldersWithOutOfSyncFiles = new HashSet<string>(this._foldersWithFailedTransferFiles);
            var newFoldersWithOutOfSyncFiles = new HashSet<string>(this.syncThingManager.TransferHistory.FailingTransfers.Select(x => x.FolderId));

            if (oldFoldersWithOutOfSyncFiles.SetEquals(newFoldersWithOutOfSyncFiles))
                return;

            this._foldersWithFailedTransferFiles.Replace(newFoldersWithOutOfSyncFiles);

            this.eventDispatcher.Raise(this.AlertsStateChanged);
        }

        private void ConflictFilesChanged(object sender, EventArgs e)
        {
            this._conflictedFiles.Replace(this.conflictFileWatcher.ConflictedFiles);
            this.eventDispatcher.Raise(this.AlertsStateChanged);
        }

        public void Dispose()
        {
            this.syncThingManager.TransferHistory.TransferCompleted -= this.TransferCompleted;
            this.conflictFileWatcher.ConflictedFilesChanged -= this.ConflictFilesChanged;
        }

        private static class EmptyReadOnlyList<T>
        {
            public static readonly IReadOnlyList<T> Instance = new List<T>().AsReadOnly();
        }
    }
}
