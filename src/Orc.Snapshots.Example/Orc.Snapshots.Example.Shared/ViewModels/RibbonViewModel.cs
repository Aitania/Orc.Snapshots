﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RibbonViewModel.cs" company="WildGums">
//   Copyright (c) 2008 - 2015 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orc.Snapshots.Example.ViewModels
{
    using System.Threading.Tasks;
    using Catel.MVVM;
    using Catel.Services;
    using Orc.Snapshots.ViewModels;

    public class RibbonViewModel : ViewModelBase
    {
        private readonly IUIVisualizerService _uiVisualizerService;
        private readonly ISnapshotManager _snapshotManager;
        private readonly IMessageService _messageService;

        public RibbonViewModel(ISnapshotManager snapshotManager, IUIVisualizerService uiVisualizerService, IMessageService messageService)
        {
            _snapshotManager = snapshotManager;
            _uiVisualizerService = uiVisualizerService;
            _messageService = messageService;

            CreateSnapshot = new TaskCommand(OnCreateSnapshotExecuteAsync, OnCreateSnapshotCanExecute);

            Title = "Orc.Snapshots example";
        }

        #region Properties
        #endregion

        #region Commands
        public TaskCommand CreateSnapshot { get; private set; }
        
        private bool OnCreateSnapshotCanExecute()
        {
            return true;
        }

        private async Task OnCreateSnapshotExecuteAsync()
        {
            var snapshot = new Snapshot();

            if (_uiVisualizerService.ShowDialog<SnapshotViewModel>(snapshot) ?? false)
            {
                var existingSnapshot = _snapshotManager.FindSnapshot(snapshot.Title);
                if (existingSnapshot != null)
                {
                    if (await _messageService.ShowAsync(
                        $"Snapshot '{snapshot}' already exists. Are you sure you want to overwrite the existing snapshot?",
                        "Are you sure?",
                        MessageButton.YesNo) != MessageResult.Yes)
                    {
                        return;
                    }

                    _snapshotManager.Remove(existingSnapshot);
                }

                _snapshotManager.Add(snapshot);

                await _snapshotManager.SaveAsync();
            }
        }
        #endregion

        #region Methods
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            await _snapshotManager.LoadAsync();
        }
        #endregion
    }
}