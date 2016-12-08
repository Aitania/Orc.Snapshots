﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SnapshotsViewModel.cs" company="WildGums">
//   Copyright (c) 2008 - 2014 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orc.Snapshots.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Catel;
    using Catel.Collections;
    using Catel.IoC;
    using Catel.Logging;
    using Catel.MVVM;
    using Catel.Services;
    using Catel.Threading;
    using Catel.Data;

    public class SnapshotsViewModel : ViewModelBase
    {
        #region Fields
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private ISnapshotManager _snapshotManager;
        private readonly IUIVisualizerService _uiVisualizerService;
        private readonly IServiceLocator _serviceLocator;
        private readonly IDispatcherService _dispatcherService;
        private readonly IMessageService _messageService;
        private readonly ILanguageService _languageService;
        #endregion

        #region Constructors
        public SnapshotsViewModel(ISnapshotManager snapshotManager, IUIVisualizerService uiVisualizerService,
            IServiceLocator serviceLocator, IDispatcherService dispatcherService, IMessageService messageService,
            ILanguageService languageService)
        {
            Argument.IsNotNull(() => snapshotManager);
            Argument.IsNotNull(() => uiVisualizerService);
            Argument.IsNotNull(() => serviceLocator);
            Argument.IsNotNull(() => dispatcherService);
            Argument.IsNotNull(() => messageService);
            Argument.IsNotNull(() => languageService);

            _snapshotManager = snapshotManager;
            _uiVisualizerService = uiVisualizerService;
            _serviceLocator = serviceLocator;
            _dispatcherService = dispatcherService;
            _messageService = messageService;
            _languageService = languageService;

            Snapshots = new List<ISnapshot>();

            RestoreSnapshot = new TaskCommand<ISnapshot>(OnRestoreSnapshotExecuteAsync, OnRestoreSnapshotCanExecute);
            EditSnapshot = new TaskCommand<ISnapshot>(OnEditSnapshotExecuteAsync, OnEditSnapshotCanExecute);
            RemoveSnapshot = new TaskCommand<ISnapshot>(OnRemoveSnapshotExecuteAsync, OnRemoveSnapshotCanExecute);
        }
        #endregion

        #region Properties
        public bool HasSnapshots { get; private set; }

        public List<ISnapshot> Snapshots { get; private set; }

        public string Filter { get; set; }

        public object Scope { get; set; }
        #endregion

        #region Commands

        public TaskCommand<ISnapshot> RestoreSnapshot { get; private set; }

        private bool OnRestoreSnapshotCanExecute(ISnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            return true;
        }

        private async Task OnRestoreSnapshotExecuteAsync(ISnapshot snapshot)
        {
            Log.Info($"Restoring snapshot '{snapshot}'");

            await _snapshotManager.RestoreSnapshotAsync(snapshot);
        }

        public TaskCommand<ISnapshot> EditSnapshot { get; private set; }

        private bool OnEditSnapshotCanExecute(ISnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            return true;
        }

        private async Task OnEditSnapshotExecuteAsync(ISnapshot snapshot)
        {
            var modelValidation = snapshot as IModelValidation;

            EventHandler<ValidationEventArgs> handler = null;
            handler = (sender, e) =>
            {
                if (_snapshotManager.Snapshots.Any(x => x.Title.EqualsIgnoreCase(snapshot.Title) && x != snapshot))
                {
                    e.ValidationContext.AddFieldValidationResult(FieldValidationResult.CreateError("Title",
                        _languageService.GetString("Snapshots_SnapshotWithCurrentTitleAlreadyExists")));
                }
            };

            if (modelValidation != null)
            {
                modelValidation.Validating += handler;
            }

            if (_uiVisualizerService.ShowDialog<SnapshotViewModel>(snapshot) ?? false)
            {
                if (modelValidation != null)
                {
                    modelValidation.Validating -= handler;
                }

                await _snapshotManager.SaveAsync();
            }
        }

        public TaskCommand<ISnapshot> RemoveSnapshot { get; private set; }

        private bool OnRemoveSnapshotCanExecute(ISnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            return true;
        }

        private async Task OnRemoveSnapshotExecuteAsync(ISnapshot snapshot)
        {
            if (await _messageService.ShowAsync(string.Format(_languageService.GetString("Snapshots_AreYouSureYouWantToRemoveTheSnapshot"), snapshot.Title),
                _languageService.GetString("Snapshots_AreYouSure"), MessageButton.YesNo, MessageImage.Question) == MessageResult.No)
            {
                return;
            }

            _snapshotManager.Remove(snapshot);

            await _snapshotManager.SaveAsync();
        }
        #endregion

        #region Methods
        private void OnFilterChanged()
        {
            UpdateSnapshots();
        }

#pragma warning disable AsyncFixer03 // Avoid fire & forget async void methods
#pragma warning disable AvoidAsyncVoid
        private async void OnScopeChanged()
        {
            var scope = Scope;

            Log.Debug($"Scope has changed to '{scope}'");

            await DeactivateSnapshotManagerAsync();
            ActivateSnapshotManager();
        }
#pragma warning restore AsyncFixer03 // Avoid fire & forget async void methods
#pragma warning restore AvoidAsyncVoid

        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            ActivateSnapshotManager();
        }

        protected override async Task CloseAsync()
        {
            await DeactivateSnapshotManagerAsync(false);

            await base.CloseAsync();
        }

        private void OnSnapshotsChanged(object sender, EventArgs e)
        {
            var snapshotManager = _snapshotManager;

            Log.Debug($"Snapshots have changed, updating snapshots, current snapshot manager scope is '{snapshotManager.Scope}'");

            UpdateSnapshots();
        }

        private ISnapshotManager GetSnapshotManager()
        {
            if (_snapshotManager == null)
            {
                var snapshotManager = _serviceLocator.ResolveType<ISnapshotManager>(Scope);
                SetSnapshotManager(snapshotManager);
            }

            return _snapshotManager;
        }

        private void SetSnapshotManager(ISnapshotManager snapshotManager)
        {
            var previousSnapshotManager = _snapshotManager;
            if (ReferenceEquals(snapshotManager, previousSnapshotManager))
            {
                return;
            }

            if (previousSnapshotManager != null)
            {
                previousSnapshotManager.SnapshotsChanged -= OnSnapshotsChanged;
            }

            Log.Debug($"Updating current snapshot manager with scope '{snapshotManager?.Scope}' to new instance with '{snapshotManager?.Snapshots.Count() ?? 0}' snapshots");

            _snapshotManager = snapshotManager;

            if (snapshotManager != null)
            {
                _snapshotManager.SnapshotsChanged += OnSnapshotsChanged;
            }
        }

        private void ActivateSnapshotManager()
        {
            var scope = Scope;

            Log.Debug($"Activating snapshot manager using scope '{scope}'");

            var snapshotManager = _serviceLocator.ResolveType<ISnapshotManager>(scope);
            SetSnapshotManager(snapshotManager);

            UpdateSnapshots();
        }

        private async Task DeactivateSnapshotManagerAsync(bool setToNull = true)
        {
            Log.Debug($"Deactivating snapshot manager");

            var snapshotManager = _snapshotManager;
            if (snapshotManager != null)
            {
                snapshotManager.SnapshotsChanged -= OnSnapshotsChanged;

                if (setToNull)
                {
                    _snapshotManager = null;
                }
            }

            Snapshots.Clear();
        }

        private void UpdateSnapshots()
        {
            var filter = Filter;

            var source = _snapshotManager.Snapshots;

            HasSnapshots = source.Any();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                source = (from item in source
                          where item.Title.ContainsIgnoreCase(filter)
                          select item);
            }

            var finalItems = new List<ISnapshot>(source);

            Log.Debug($"Updating available snapshots using snapshot manager with scope '{_snapshotManager?.Scope}', '{finalItems.Count}' snapshots available");

            Snapshots = finalItems;
        }
        #endregion
    }
}