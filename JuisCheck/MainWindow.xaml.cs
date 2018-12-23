﻿/*
 * Program   : JuisCheck for Windows
 * Copyright : Copyright (C) 2018 Roger Hünen
 * License   : GNU General Public License version 3 (see LICENSE)
 */

using Fluent;
using Microsoft.Win32;
using Muon.DotNetExtensions;
using Muon.Windows;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

using JuisCheck.Lang;
using JuisCheck.Properties;

namespace JuisCheck
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public sealed partial class MainWindow : RibbonWindow
	{
		public DeviceCollection	Devices		{ get; private set; } = new DeviceCollection();
		public Settings			AppSettings	{ get; private set; } = Settings.Default;

		public MainWindow()
		{
			DataContext = this;
			InitializeComponent();

			Backstage_Init();
			Devices_Init();

			RestoreWindowMetrics();
			SetWindowTitle();

			AppSettings.PropertyChanged += Settings_PropertyChanged_Handler;
			Devices.IsModifiedChanged   += Devices_IsModifiedChanged_Handler;
		}

		private void CloseDeviceCollection()
		{
			if (SaveUnsavedData()) {
				Devices.Empty();
				Devices_InitSorting();
			}
		}

		private bool IsInfinityOrNaN( double number )
		{
			return (double.IsInfinity(number) || double.IsNaN(number));
		}

		private void OpenDeviceCollection( string fileName = null )
		{
			if (fileName != null && string.Compare(fileName, Devices.FileName, true) == 0) {
				return;
			}

			if (!SaveUnsavedData()) {
				return;
			}

			if (fileName == null) {
				OpenFileDialog ofd = new OpenFileDialog() {
					AddExtension     = false,
					CheckFileExists  = true,
					CheckPathExists  = true,
					Filter           = "XML files (*.xml)|*.xml",
					InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					ReadOnlyChecked  = false,
					ShowReadOnly     = false,
					Title            = JCstring.dialogCaptionOpen,
					ValidateNames    = true
				};

				if (ofd.ShowDialog(this) != true ) {
					return;
				}

				fileName = ofd.FileName;
			}

			try {
				Devices.Load(fileName);
				Devices_InitSorting();
				RecentFiles.Add(fileName);
			}
			catch ( Exception ex ) {
				ShowErrorMessage(string.Format(JCstring.messageTextOpenDeviceCollectionFailed.Unescape(), ex.Message));
			}
		}

		private void RestoreWindowMetrics()
		{
			if (AppSettings.MainWindowRestoreMetrics) {
				Rectangle restoreBounds = new Rectangle(int.MinValue, int.MinValue, int.MinValue, int.MinValue);

				if (!IsInfinityOrNaN(AppSettings.MainWindowLeft  )) { restoreBounds.X      = (int)AppSettings.MainWindowLeft;   }
				if (!IsInfinityOrNaN(AppSettings.MainWindowTop   )) { restoreBounds.Y      = (int)AppSettings.MainWindowTop;    }
				if (!IsInfinityOrNaN(AppSettings.MainWindowWidth )) { restoreBounds.Width  = (int)AppSettings.MainWindowWidth;  }
				if (!IsInfinityOrNaN(AppSettings.MainWindowHeight)) { restoreBounds.Height = (int)AppSettings.MainWindowHeight; }

				// Window position: prevent window from being positioned off screen

				bool intersectsWithScreen = false;
				foreach (WinForms.Screen screen in WinForms.Screen.AllScreens) {
					if (screen.WorkingArea.IntersectsWith(restoreBounds)) {
						intersectsWithScreen = true;
					}
				}

				if (intersectsWithScreen) {
					if (restoreBounds.Left != int.MinValue) { Left = restoreBounds.Left; }
					if (restoreBounds.Top  != int.MinValue) { Top  = restoreBounds.Top;  }
				}

				// Window size

				if (restoreBounds.Width  != int.MinValue) { Width  = restoreBounds.Width;  }
				if (restoreBounds.Height != int.MinValue) { Height = restoreBounds.Height; }

				// Window state

				WindowState = AppSettings.MainWindowMaximized ? WindowState.Maximized : WindowState.Normal;
			}
		}

		private bool SaveDeviceCollection()
		{
			try {
				Devices.Save();
				return true;
			} catch {
			}

			return SaveDeviceCollectionAs();
		}

		private bool SaveDeviceCollectionAs()
		{
			SaveFileDialog sfd = new SaveFileDialog {
				AddExtension    = true, 
				CheckFileExists = false,
				CheckPathExists = true,
				CreatePrompt    = false,
				DefaultExt      = "xml",
				Filter          = "XML files (*.xml)|*.xml",
				OverwritePrompt = true,
				Title           = JCstring.dialogCaptionSave,
				ValidateNames   = true
			};

			if (Devices.FileName == null) {
				sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				sfd.FileName         = string.Empty;
			} else {
				sfd.InitialDirectory = Path.GetDirectoryName(Devices.FileName);
				sfd.FileName         = Path.GetFileName(Devices.FileName);
			}

			if (sfd.ShowDialog(this) != true) {
				return false;
			}

			try {
				Devices.Save(sfd.FileName);
				RecentFiles.Add(sfd.FileName);
				return true;
			}
			catch ( Exception ex ) {
				ShowErrorMessage(string.Format(JCstring.messageTextSaveDeviceCollectionFailed.Unescape(), ex.Message));
				return false;
			}
		}

		private void SaveWindowMetrics()
		{
			if (!IsInfinityOrNaN(RestoreBounds.Left  )) { AppSettings.MainWindowLeft   = RestoreBounds.Left;   }
			if (!IsInfinityOrNaN(RestoreBounds.Top   )) { AppSettings.MainWindowTop    = RestoreBounds.Top;    }
			if (!IsInfinityOrNaN(RestoreBounds.Width )) { AppSettings.MainWindowWidth  = RestoreBounds.Width;  }
			if (!IsInfinityOrNaN(RestoreBounds.Height)) { AppSettings.MainWindowHeight = RestoreBounds.Height; }
			AppSettings.MainWindowMaximized = WindowState == WindowState.Maximized;
		}

		private bool SaveUnsavedData()
		{
			if (!Devices.IsModified) {
				return true;
			}

			while (true) {
				MessageBoxExResult result =	MessageBoxEx.Show(
					new MessageBoxExParams {
						CaptionText = JCstring.messageCaptionUnsavedData,
						MessageText = JCstring.messageTextUnsavedData.Unescape(),
						Image       = MessageBoxExImage.Warning,
						Button      = MessageBoxExButton.YesNoCancel,
						Owner       = this
					}
				);

				switch (result) {
					case MessageBoxExResult.Yes:
						if (SaveDeviceCollection()) {
							return true;
						}
						break;

					case MessageBoxExResult.No:
						return true;

					case MessageBoxExResult.Cancel:
						return false;
				}
			}
		}

		private void SetDataGridFocus()
		{
			Devices_SetDataGridFocus();
		}

		private void SetWindowTitle()
		{
			string modifiedInfo = Devices.IsModified ? "*" : string.Empty;
			string fileInfo     = Devices.FileName ?? JCstring.fileNameNew;

			Title = string.Format("{0} - {1} {2}", modifiedInfo + fileInfo, JCstring.programName, Assembly.GetExecutingAssembly().GetVersion(3));
		}

		private void ShowErrorMessage( string message )
		{
			MessageBoxEx.Show(
				new MessageBoxExParams {
					CaptionText = JCstring.messageCaptionError,
					MessageText = message,
					Image       = MessageBoxExImage.Error,
					Button      = MessageBoxExButton.OK,
					Owner       = this
				}
			);
		}

		// Event: Closing
		
		private void Closing_Handler( object sender, CancelEventArgs evt )
		{
			if (!SaveUnsavedData()) {
				evt.Cancel = true;
				return;
			}

			SaveWindowMetrics();
			evt.Cancel = false;
		}

		// Event: Devices_IsModifiedChanged

		private void Devices_IsModifiedChanged_Handler( object sender, EventArgs evt )
		{
			SetWindowTitle();
		}

		// Event: Settings_PropertyChanged

		private void Settings_PropertyChanged_Handler( object sender, PropertyChangedEventArgs evt )
		{
			switch (evt.PropertyName) {
				case nameof(AppSettings.JuisReleaseShowBuildNumber):
					Devices_RefreshView();
					break;

				case nameof(AppSettings.RecentFilesMax):
					Backstage_PopulateRecentFiles();
					break;
			}
		}
	}
}