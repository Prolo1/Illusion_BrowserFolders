using AIChara;
using CharaCustom;
using HarmonyLib;
using KKAPI.Maker;
using KKAPI.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace BrowserFolders
{
	public class MakerOutfitFolders : IFolderBrowser
	{
		private static CvsC_ClothesLoad _charaLoad;
		private static CanvasGroup[] _charaLoadVisible;
		private static CvsC_ClothesSave _charaSave;
		private static CanvasGroup[] _charaSaveVisible;
		private static GameObject _makerCanvas;

		private static VisibleWindow _lastRefreshed;
		private static FolderTreeView _folderTreeView;

		private bool _guiActive;

		public MakerOutfitFolders()
		{
			_folderTreeView = new FolderTreeView(AI_BrowserFolders.UserDataPath, AI_BrowserFolders.UserDataPath)
			{
				CurrentFolderChanged = RefreshCurrentWindow
			};

			Harmony.CreateAndPatchAll(typeof(MakerOutfitFolders));
		}
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ChaFileCoordinate), "SaveFile")]
		internal static void SaveFilePatch(ref string path)
		{
			try
			{
				if(_makerCanvas == null) return;
				var newFolder = _folderTreeView?.CurrentFolder;
				if(newFolder == null) return;

				var name = Path.GetFileName(path);
				path = Path.Combine(newFolder, name);

				// Force reload
				_lastRefreshed = VisibleWindow.None;
			}
			catch(Exception ex)
			{
				UnityEngine.Debug.LogError(ex);
			}
		}

		private static VisibleWindow IsVisible()
		{
			if(_makerCanvas == null) return VisibleWindow.None;
			if(!_makerCanvas.activeSelf) return VisibleWindow.None;
			if(IsLoadVisible()) return VisibleWindow.Load;
			if(IsSaveVisible()) return VisibleWindow.Save;
			return VisibleWindow.None;

			bool IsSaveVisible()
			{
				return _charaSave != null && _charaSaveVisible.All(x => x.interactable);
			}

			bool IsLoadVisible()
			{
				return _charaLoad != null && _charaLoadVisible.All(x => x.interactable);
			}
		}

		private static string GetCurrentFolder(string defaultPath)
		{
			if(IsVisible() != VisibleWindow.None)
			{
				var overrideFolder = _folderTreeView?.CurrentFolder;
				if(overrideFolder != null) return overrideFolder + '/';
			}
			return defaultPath;
		}

		private static void RefreshCurrentWindow()
		{
			var visibleWindow = IsVisible();
			_lastRefreshed = visibleWindow;
			var resetTree = false;

			switch(visibleWindow)
			{
			case VisibleWindow.Load:
				if(_charaLoad != null)
				{
					_charaLoad.UpdateClothesList();
					resetTree = true;
				}
				break;
			case VisibleWindow.Save:
				if(_charaSave != null)
				{
					_charaSave.UpdateClothesList();
					resetTree = true;
				}
				break;
			}

			// clear tree cache
			if(resetTree) _folderTreeView.ResetTreeCache();
		}

		public void OnGui()
		{
			//todo  When loading a coordinate it resets to the main folder without deselect in menu
			var visibleWindow = IsVisible();
			if(visibleWindow == VisibleWindow.None)
			{
				_lastRefreshed = VisibleWindow.None;
				if(_guiActive)
				{
					_folderTreeView?.StopMonitoringFiles();
					_guiActive = false;
				}
				return;
			}

			_guiActive = true;
			if(_lastRefreshed != visibleWindow) RefreshCurrentWindow();

			var screenRect = MakerFolders.GetDisplayRect();
			IMGUIUtils.DrawSolidBox(screenRect);
			GUILayout.Window(362, screenRect, TreeWindow, "Select clothes folder");
			IMGUIUtils.EatInputInRect(screenRect);
		}

		private static void TreeWindow(int id)
		{
			GUILayout.BeginVertical();
			{
				_folderTreeView.DrawDirectoryTree();

				GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
				{
					if(GUILayout.Button("Refresh thumbnails"))
					{
						_folderTreeView?.ResetTreeCache();
						RefreshCurrentWindow();
					}

					GUILayout.Space(1);

					if(GUILayout.Button("Current folder"))
						Utils.OpenDirInExplorer(_folderTreeView.CurrentFolder);
					if(GUILayout.Button("Screenshot folder"))
						Utils.OpenDirInExplorer(Path.Combine(Utils.NormalizePath(UserData.Path), "cap"));
					if(GUILayout.Button("Main game folder"))
						Utils.OpenDirInExplorer(Path.GetDirectoryName(Utils.NormalizePath(UserData.Path)));
				}
				GUILayout.EndVertical();
			}
			GUILayout.EndVertical();
		}

		private enum VisibleWindow
		{
			None,
			Load,
			Save,
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CvsC_ClothesLoad), "Start")]
		internal static void InitHookLoad(CvsC_ClothesLoad __instance)
		{
			_folderTreeView.DefaultPath = Path.Combine(Utils.NormalizePath(UserData.Path),
				MakerAPI.GetMakerSex() == 0 ? "coordinate/male" : @"coordinate/female");
			_folderTreeView.CurrentFolder = _folderTreeView.DefaultPath;
			//_targetScene = GetAddSceneName();

			_makerCanvas = __instance.GetComponentInParent<Canvas>().gameObject;

			_charaLoad = __instance;
			_charaLoadVisible = __instance.GetComponentsInParent<CanvasGroup>(true);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CvsC_ClothesSave), "Start")]
		internal static void InitHookSave(CvsC_ClothesSave __instance)
		{
			_charaSave = __instance;
			_charaSaveVisible = __instance.GetComponentsInParent<CanvasGroup>(true);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CustomCharaFileInfoAssist),
			nameof(CustomCharaFileInfoAssist.AddList))]
		private static bool FixedAddList(List<CustomClothesFileInfo> _list, string path, byte sex, bool preset, ref int idx)
#if HS2
		{
			path = GetCurrentFolder(path);// you don't need the transpiller to just do this

			string[] array = new string[] { "*.png" };
			CoordinateCategoryKind coordinateCategoryKind = ((sex == 0) ? CoordinateCategoryKind.Male : CoordinateCategoryKind.Female);
			if(preset)
			{
				coordinateCategoryKind |= CoordinateCategoryKind.Preset;
			}
			FolderAssist folderAssist = new FolderAssist();
			folderAssist.CreateFolderInfoEx(path, array, true);
			int fileCount = folderAssist.GetFileCount();
			ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
			for(int i = 0; i < fileCount; i++)
			{
				if(!chaFileCoordinate.LoadFile(folderAssist.lstFile[i].FullPath))
					chaFileCoordinate.GetLastErrorCode();

				else
				{
					_list.Add(new CustomClothesFileInfo()
					{
						index = idx++,
						name = chaFileCoordinate.coordinateName,
						FullPath = folderAssist.lstFile[i].FullPath,
						FileName = folderAssist.lstFile[i].FileName,
						time = folderAssist.lstFile[i].time,
						cateKind = coordinateCategoryKind,
					});
				}
			}

			UnityEngine.Resources.UnloadUnusedAssets();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, false);

			return false;
		}
#else
		{
			path = GetCurrentFolder(path);// you don't need the transpiller to just do this

			string[] array = new string[] { "*.png" };
			CoordinateCategoryKind coordinateCategoryKind = ((sex != 0) ? CoordinateCategoryKind.Female : CoordinateCategoryKind.Male);
			if(preset)
			{
				coordinateCategoryKind |= CoordinateCategoryKind.Preset;
			}
			FolderAssist folderAssist = new FolderAssist();
			folderAssist.CreateFolderInfoEx(path, array, true);
			int fileCount = folderAssist.GetFileCount();
			ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
			for(int i = 0; i < fileCount; i++)
			{
				if(chaFileCoordinate.LoadFile(folderAssist.lstFile[i].FullPath))
				{
					_list.Add(new CustomClothesFileInfo
					{
						index = idx++,
						name = chaFileCoordinate.coordinateName,
						FullPath = folderAssist.lstFile[i].FullPath,
						FileName = folderAssist.lstFile[i].FileName,
						time = folderAssist.lstFile[i].time,
						cateKind = coordinateCategoryKind
					});
				}
			}


			UnityEngine.Resources.UnloadUnusedAssets();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, false);
			return false;
		}
#endif

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CustomClothesFileInfoAssist), nameof(CustomClothesFileInfoAssist.AddList))]
		internal static void InitializePatch(ref string __1)
		{
			__1 = GetCurrentFolder(__1);
		}
	}
}