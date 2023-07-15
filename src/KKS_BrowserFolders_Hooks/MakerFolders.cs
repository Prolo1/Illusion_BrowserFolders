using System;
using System.Linq;
using System.IO;
using ChaCustom;
using HarmonyLib;
using HarmonyLib.Tools;
using KKAPI.Maker;
using KKAPI.Utilities;
using Manager;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using ExtensibleSaveFormat;
using BepInEx;
using BepInEx.Logging;

namespace BrowserFolders.Hooks.KKS
{
	[BrowserType(BrowserType.Maker)]
	public class MakerFolders : IFolderBrowser
	{
		private static Toggle _catToggle;
		private static CustomCharaFile _customCharaFile;
		private static FolderTreeView _folderTreeView;
		private static Toggle _loadCharaToggle;
		private static Toggle _saveCharaToggle;
		private static GameObject _saveFront;
		private static GameObject _ccwGo;

		public static string CurrentRelativeFolder => _folderTreeView?.CurrentRelativeFolder;

		private static bool _refreshList;
		private static string _targetScene;

		public MakerFolders()
		{
			_folderTreeView = new FolderTreeView(Utils.NormalizePath(UserData.Path), Utils.NormalizePath(UserData.Path))
			{
				CurrentFolderChanged = OnFolderChanged
			};

			Harmony.CreateAndPatchAll(typeof(MakerFolders));

			MakerCardSave.RegisterNewCardSavePathModifier(DirectoryPathModifier, null);

			Overlord.Init();
		}

		private static string DirectoryPathModifier(string currentDirectoryPath)
		{
			return _folderTreeView != null ? _folderTreeView.CurrentFolder : currentDirectoryPath;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CustomCharaFile), nameof(CustomCharaFile.Initialize))]
		public static void InitializePatch(CustomCharaFile __instance)
		{
			if(_customCharaFile == null)
			{
				_customCharaFile = __instance;

				_folderTreeView.DefaultPath = Overlord.GetDefaultPath(__instance.chaCtrl.sex);
				_folderTreeView.CurrentFolder = _folderTreeView.DefaultPath;

				_targetScene = Scene.AddSceneName;
			}


		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CustomCharaFile), "Start")]
		public static void InitHook(CustomCharaFile __instance)
		{
			var gt = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/06_SystemTop");
			_loadCharaToggle = gt.transform.Find("tglLoadChara").GetComponent<Toggle>();
			_saveCharaToggle = gt.transform.Find("tglSaveChara").GetComponent<Toggle>();

			var mt = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMainMenu/BaseTop/tglSystem");
			_catToggle = mt.GetComponent<Toggle>();

			_saveFront = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CvsCaptureFront");

			_targetScene = Scene.AddSceneName;

			// Exit maker / save character dialog boxes
			_ccwGo = GameObject.FindObjectOfType<CustomCheckWindow>()?.gameObject;
		}

		public void OnGui()
		{
			var guiShown = false;
			// Check the opened category
			if(_catToggle != null && _catToggle.isOn && _targetScene == Scene.AddSceneName)
			{
				// Check opened tab
				if(_loadCharaToggle != null && _loadCharaToggle.isOn || _saveCharaToggle != null && _saveCharaToggle.isOn)
				{
					// Check if the character picture take screen is displayed
					if((_saveFront == null || !_saveFront.activeSelf) && !Scene.IsOverlap && !Scene.IsNowLoadingFade && (_ccwGo == null || !_ccwGo.activeSelf))
					{
						if(_refreshList)
						{
							_folderTreeView.ResetTreeCache();
							OnFolderChanged();
							_refreshList = false;
						}

						var screenRect = new Rect((int)(Screen.width * 0.004), (int)(Screen.height * 0.57f), (int)(Screen.width * 0.125), (int)(Screen.height * 0.35));
						IMGUIUtils.DrawSolidBox(screenRect);
						GUILayout.Window(362, screenRect, TreeWindow, "Select character folder");
						IMGUIUtils.EatInputInRect(screenRect);
						guiShown = true;
					}
				}
			}

			if(!guiShown)
			{
				_folderTreeView?.StopMonitoringFiles();
			}
		}

		private static void OnFolderChanged()
		{
			if(_customCharaFile == null) return;

			var loadCharaToggleIsOn = _loadCharaToggle != null && _loadCharaToggle.isOn;
			if(loadCharaToggleIsOn || _saveCharaToggle != null && _saveCharaToggle.isOn)
			{
				_customCharaFile.Initialize(loadCharaToggleIsOn == true, false);
			}
		}

		private static void TreeWindow(int id)
		{
			GUILayout.BeginVertical();
			{
				_folderTreeView.DrawDirectoryTree();

				GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
				{
					if(Overlord.DrawDefaultCardsToggle())
						OnFolderChanged();

					if(GUILayout.Button("Refresh thumbnails"))
					{
						_folderTreeView.ResetTreeCache();
						OnFolderChanged();
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




		/*marco*/
		/*
		[HarmonyPrefix]
		[HarmonyPatch(typeof(Localize.Translate.Manager), nameof(Localize.Translate.Manager.CreateChaFileInfo))]
		private static bool FixedCreateChaFileInfo(int sex, bool useDefaultData, ref Localize.Translate.Manager.ChaFileInfo[] __result)
		{
			var fileInfo = Localize.Translate.Manager.DefaultData.UserDataAssist(sex == 0 ? "chara/male/" : "chara/female/", useDefaultData);

			var results = new List<Localize.Translate.Manager.ChaFileInfo>(fileInfo.Length);
				var chaFileControl = new ChaFileControl();

			for(int i = 0; i < fileInfo.Length; i++)
			{
				var file = fileInfo[i];

				var success = chaFileControl.LoadCharaFile(file.info.FullPath, byte.MaxValue, false, true);
				if(success && chaFileControl.parameter.sex == sex)
				{
					chaFileControl.CopyAll(chaFileControl);
					results.Add(new Localize.Translate.Manager.ChaFileInfo(chaFileControl, file));

					chaFileControl = new ChaFileControl();
				}
			}
			__result = results.ToArray();


			UnityEngine.Resources.UnloadUnusedAssets();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, false);


			return false;
		}
		*/


		/**<summary>mine</summary>**/
		[HarmonyPrefix]
		[HarmonyPatch(typeof(Localize.Translate.Manager), nameof(Localize.Translate.Manager.CreateChaFileInfo))]
		private static bool FixedCreateChaFileInfo(int sex, bool useDefaultData, ref Localize.Translate.Manager.ChaFileInfo[] __result)
		{
			{

				ChaFileControl chaFileControl = new ChaFileControl();
				var fileInfo = Localize.Translate.Manager.DefaultData.UserDataAssist((sex == 0) ? "chara/male/" : "chara/female/", useDefaultData);

				//testing now			
				List<ChaFileControl> chaFileControls = new List<ChaFileControl>(fileInfo.Length);//seems to help with memory collection ¯\_(ツ)_/¯
				var variables = fileInfo.Attempt((file) =>
				{
					bool flag = chaFileControl.LoadCharaFile(file.info.FullPath, byte.MaxValue, false, true);
					if(flag && (int)chaFileControl.parameter.sex == sex)
					{
						chaFileControls.Add(new ChaFileControl());
						chaFileControls.Last().CopyAll(chaFileControl);
						return new Localize.Translate.Manager.ChaFileInfo(chaFileControls.Last(), file);
					}

					throw new Exception();
				});
				__result = variables.ToArray();


				Resources.UnloadUnusedAssets();
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, false);
			}

			return false;
		}

		/*
		[HarmonyPrefix]
		[HarmonyPatch(typeof(Localize.Translate.FileListControler), nameof(Localize.Translate.FileListControler.Execute))]
		private static bool FixedExecute(int sex, Localize.Translate.Manager.ChaFileInfo[] fileInfo, CustomFileListCtrl listCtrl)
		{

			listCtrl.ClearList();
			if(sex == 0)
			{
				listCtrl.visibleType = FileListUI.VisibleType.AddHide;
				foreach(var val in fileInfo.
					Select((Localize.Translate.Manager.ChaFileInfo p, int index) =>
					new { p, index }))
				{

					var p3 = val.p;
					ChaFileParameter parameter = p3.chaFile.parameter;
					FolderAssist.FileInfo info = p3.info;
					listCtrl.AddList(new CustomFileInfo(info)
					{
						index = val.index,
						name = parameter.fullname,
						isDefaultData = p3.isDefault,
						isMyData = (p3.chaFile.about.userID == GameSystem.UserUUID)
					});
				}

				return false;
			}

			foreach(var val in fileInfo.Select((Localize.Translate.Manager.ChaFileInfo p, int index) => new { p, index }))
			{
				var p2 = val.p;
				ChaFileParameter parameter2 = p2.chaFile.parameter;
				FolderAssist.FileInfo info2 = p2.info;
				listCtrl.AddList(new CustomFileInfo(info2)
				{
					index = val.index,
					name = parameter2.fullname,
					club = Localize.Translate.Manager.GetClubName((int)parameter2.clubActivities, false),
					personality = Localize.Translate.Manager.GetPersonalityName(parameter2.personality, false),
					isDefaultData = p2.isDefault,
					isMyData = (p2.chaFile.about.userID == GameSystem.UserUUID)
				});
			}


			return false;
		}
		*/
	}
}
