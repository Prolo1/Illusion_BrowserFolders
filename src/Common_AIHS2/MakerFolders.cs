using AIChara;
#if AI
using AIProject.SaveData;
#endif
using CharaCustom;
using HarmonyLib;
using KKAPI.Maker;
using KKAPI.Utilities;
using Manager;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BrowserFolders
{
	public class MakerFolders : IFolderBrowser
	{
		private static CvsO_CharaLoad _charaLoad;
		private static CanvasGroup[] _charaLoadVisible;
		private static CvsO_CharaSave _charaSave;
		private static CanvasGroup[] _charaSaveVisible;
		private static CvsO_Fusion _charaFusion;
		private static CanvasGroup[] _charaFusionVisible;
		private static GameObject _makerCanvas;

		private static VisibleWindow _lastRefreshed;
		private static FolderTreeView _folderTreeView;

		private bool _guiActive;

		public MakerFolders()
		{
			_folderTreeView = new FolderTreeView(AI_BrowserFolders.UserDataPath, AI_BrowserFolders.UserDataPath)
			{
				CurrentFolderChanged = RefreshCurrentWindow
			};

			Harmony.CreateAndPatchAll(typeof(MakerFolders));

			//Harmony.CreateAndPatchAll(typeof(CharaListMemLeakFix));

			MakerCardSave.RegisterNewCardSavePathModifier(CardSavePathModifier, null);
		}

		private static VisibleWindow IsVisible()
		{
			if(_makerCanvas == null) return VisibleWindow.None;
			if(IsFusionVisible()) return VisibleWindow.Fuse;
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

			bool IsFusionVisible()
			{
				return _charaFusion != null && _charaFusionVisible.All(x => x.interactable);
			}
		}

		private static string CardSavePathModifier(string currentDirectoryPath)
		{
			if(_makerCanvas == null) return currentDirectoryPath;
			var newFolder = _folderTreeView?.CurrentFolder;
			if(newFolder != null)
			{
				// Force reload
				_lastRefreshed = VisibleWindow.None;
				return newFolder;
			}

			return currentDirectoryPath;
		}

		private static string GetCurrentFolder(string defaultPath)
		{
			if(IsVisible() == VisibleWindow.None) return defaultPath;
			return _folderTreeView?.CurrentFolder ?? defaultPath;
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
					_charaLoad.UpdateCharasList();

					resetTree = true;
				}
				break;
			case VisibleWindow.Save:
				if(_charaSave != null)
				{
					_charaSave.UpdateCharasList();

					resetTree = true;
				}
				break;
			case VisibleWindow.Fuse:
				if(_charaFusion != null)
				{
					_charaFusion.UpdateCharasList();

					resetTree = true;
				}

				break;
			}

			// clear tree cache
			if(resetTree) _folderTreeView.ResetTreeCache();

		}

		internal static Rect GetDisplayRect()
		{
#if HS2
			const float x = 0.623f;
#elif AI
			const float x = 0.607f;
#endif
			const float y = 0.17f;
			const float w = 0.125f;
			const float h = 0.4f;

			return new Rect((int)(Screen.width * x), (int)(Screen.height * y),
				(int)(Screen.width * w), (int)(Screen.height * h));
		}

		public void OnGui()
		{
			var visibleWindow = IsVisible();
			if(visibleWindow == VisibleWindow.None)
			{
				_lastRefreshed = VisibleWindow.None;
				if(!_guiActive)
				{
					_folderTreeView?.StopMonitoringFiles();
					_guiActive = false;
				}
				return;
			}

			_guiActive = true;
			if(_lastRefreshed != visibleWindow) RefreshCurrentWindow();

			var screenRect = GetDisplayRect();
			IMGUIUtils.DrawSolidBox(screenRect);
			GUILayout.Window(362, screenRect, TreeWindow, "Select character folder");
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
			Fuse
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CvsO_CharaLoad), "Start")]
		internal static void InitHookLoad(CvsO_CharaLoad __instance)
		{
			_folderTreeView.DefaultPath = Path.Combine(Utils.NormalizePath(UserData.Path),
				MakerAPI.GetMakerSex() == 0 ? "chara/male" : @"chara/female");
			_folderTreeView.CurrentFolder = _folderTreeView.DefaultPath;
			//_targetScene = GetAddSceneName();

			_makerCanvas = __instance.GetComponentInParent<Canvas>().gameObject;

			_charaLoad = __instance;
			_charaLoadVisible = __instance.GetComponentsInParent<CanvasGroup>(true);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CvsO_CharaSave), "Start")]
		internal static void InitHookSave(CvsO_CharaSave __instance)
		{
			_charaSave = __instance;
			_charaSaveVisible = __instance.GetComponentsInParent<CanvasGroup>(true);
		}

#if AI
		[HarmonyPostfix]
		[HarmonyPatch(typeof(CvsO_Fusion), "Start")]
		internal static void InitHookFuse(CvsO_Fusion __instance, Button ___btnFusion,
			CustomCharaWindow ___charaLoadWinA, CustomCharaWindow ___charaLoadWinB)
		{
			InitFusion(__instance, ___btnFusion, ___charaLoadWinA, ___charaLoadWinB);
		}
#elif HS2
		[HarmonyPostfix]
		[HarmonyPatch(typeof(CvsO_Fusion), "Start")]
		internal static void InitHookFuse(CvsO_Fusion __instance, Button ___btnFusion,
			CustomCharaWindow ___charaLoadWinA, CustomCharaWindow ___charaLoadWinB, ref IEnumerator __result)
		{
			__result = __result.AppendCo(() => InitFusion(__instance, ___btnFusion, ___charaLoadWinA, ___charaLoadWinB));
		}




#endif


		[HarmonyPrefix]
		[HarmonyPatch(typeof(CustomCharaFileInfoAssist),
			nameof(CustomCharaFileInfoAssist.AddList))]
		private static bool FixedAddList(List<CustomCharaFileInfo> _list, string path, byte sex, bool useMyData, bool useDownload, bool preset, bool _isFindSaveData, ref int idx)

#if HS2
		{

			path = GetCurrentFolder(path);// you don't need the transpiller to just do this

			string[] array = new string[] { "*.png" };
			string userUUID = Singleton<GameSystem>.Instance.UserUUID;
			CharaCategoryKind charaCategoryKind = ((sex == 0) ? CharaCategoryKind.Male : CharaCategoryKind.Female);
			if(preset)
			{
				charaCategoryKind |= CharaCategoryKind.Preset;
			}
			FolderAssist folderAssist = new FolderAssist();
			folderAssist.CreateFolderInfoEx(path, array, true);
			int fileCount = folderAssist.GetFileCount();
			ChaFileControl chaFileControl = new ChaFileControl();//I moved this line...
			for(int i = 0; i < fileCount; i++)
			{
				//FROM HERE!!! And it fixed it!!!
				if(!chaFileControl.LoadCharaFile(folderAssist.lstFile[i].FullPath, 255, false, true))
				{
					chaFileControl.GetLastErrorCode();
				}
				else if(chaFileControl.parameter.sex == sex)
				{
					CharaCategoryKind charaCategoryKind2 = (CharaCategoryKind)0;
					if(!preset)
					{
						if(userUUID == chaFileControl.userID)
						{
							if(!useMyData)
							{
								continue;
							}
							charaCategoryKind2 = CharaCategoryKind.MyData;
						}
						else
						{
							if(!useDownload)
							{
								continue;
							}
							charaCategoryKind2 = CharaCategoryKind.Download;
						}
					}
					string text;
					if(sex != 0)
					{
						VoiceInfo.Param param;
						if(!Voice.infoTable.TryGetValue(chaFileControl.parameter2.personality, out param))
						{
							text = "不明";
						}
						else
						{
							text = param.Get(Singleton<GameSystem>.Instance.languageInt);
						}
					}
					else
					{
						text = "";
					}

					
					_list.Add(new CustomCharaFileInfo()
					{

						index = idx++,
						name = chaFileControl.parameter.fullname,
						personality = text,
						voice = chaFileControl.parameter2.personality,
						height = chaFileControl.custom.GetHeightKind(),
						bustSize = chaFileControl.custom.GetBustSizeKind(),
						hair = chaFileControl.custom.hair.kind,
						birthMonth = (int)chaFileControl.parameter.birthMonth,
						birthDay = (int)chaFileControl.parameter.birthDay,
						strBirthDay = ChaFileDefine.GetBirthdayStr((int)chaFileControl.parameter.birthMonth, (int)chaFileControl.parameter.birthDay, Singleton<GameSystem>.Instance.language),
						sex = (int)chaFileControl.parameter.sex,
						FullPath = folderAssist.lstFile[i].FullPath,
						FileName = folderAssist.lstFile[i].FileName,
						time = folderAssist.lstFile[i].time,
						isChangeParameter = chaFileControl.gameinfo2.isChangeParameter,
						trait = (int)chaFileControl.parameter2.trait,
						mind = (int)chaFileControl.parameter2.mind,
						hAttribute = (int)chaFileControl.parameter2.hAttribute,
						futanari = chaFileControl.parameter.futanari,
						cateKind = charaCategoryKind | charaCategoryKind2,
						data_uuid = chaFileControl.dataID,
						isInSaveData = _isFindSaveData && SaveData.IsRoomListChara(folderAssist.lstFile[i].FileName),

					});
				}

			}
			UnityEngine.Resources.UnloadUnusedAssets();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, false);

			return false;//stop the original code from running
		}
#else
		{
			path = GetCurrentFolder(path);// you don't need the transpiller to just do this


			string[] array = new string[] { "*.png" };
			List<string> list = new List<string>();
			if(_isFindSaveData && Singleton<Game>.Instance.Data != null)
			{
				WorldData autoData = Singleton<Game>.Instance.Data.AutoData;
				if(autoData != null)
				{
					list.Add(autoData.PlayerData.CharaFileName);
					foreach(KeyValuePair<int, AgentData> keyValuePair in autoData.AgentTable)
					{
						list.Add(keyValuePair.Value.CharaFileName);
					}
				}
				foreach(KeyValuePair<int, WorldData> keyValuePair2 in Singleton<Game>.Instance.Data.WorldList)
				{
					list.Add(keyValuePair2.Value.PlayerData.CharaFileName);
					foreach(KeyValuePair<int, AgentData> keyValuePair3 in keyValuePair2.Value.AgentTable)
					{
						list.Add(keyValuePair3.Value.CharaFileName);
					}
				}
			}
			string userUUID = Singleton<GameSystem>.Instance.UserUUID;
			CharaCategoryKind charaCategoryKind = ((sex != 0) ? CharaCategoryKind.Female : CharaCategoryKind.Male);
			if(preset)
			{
				charaCategoryKind |= CharaCategoryKind.Preset;
			}
			FolderAssist folderAssist = new FolderAssist();
			folderAssist.CreateFolderInfoEx(path, array, true);
			int fileCount = folderAssist.GetFileCount();
			ChaFileControl chaFileControl = new ChaFileControl();//Just one line
			for(int i = 0; i < fileCount; i++)
			{
				if(!chaFileControl.LoadCharaFile(folderAssist.lstFile[i].FullPath, 255, false, true))
				{
					int lastErrorCode = chaFileControl.GetLastErrorCode();
				}
				else if(chaFileControl.parameter.sex == sex)
				{
					CharaCategoryKind charaCategoryKind2 = (CharaCategoryKind)0;
					if(!preset)
					{
						if(userUUID == chaFileControl.userID)
						{
							if(!useMyData)
								continue;

							charaCategoryKind2 = CharaCategoryKind.MyData;
						}
						else
						{
							if(!useDownload)
								continue;

							charaCategoryKind2 = CharaCategoryKind.Download;
						}
					}
					string text = string.Empty;
					if(sex != 0)
					{
						VoiceInfo.Param param;
						if(!Singleton<Voice>.Instance.voiceInfoDic.TryGetValue(chaFileControl.parameter.personality, out param))
						{
							text = "不明";
						}
						else
						{
							text = param.Personality;
						}
					}
					else
					{
						text = string.Empty;
					}
					_list.Add(new CustomCharaFileInfo
					{
						index = idx++,
						name = chaFileControl.parameter.fullname,
						personality = text,
						type = chaFileControl.parameter.personality,
						height = chaFileControl.custom.GetHeightKind(),
						bustSize = chaFileControl.custom.GetBustSizeKind(),
						hair = chaFileControl.custom.hair.kind,
						birthMonth = (int)chaFileControl.parameter.birthMonth,
						birthDay = (int)chaFileControl.parameter.birthDay,
						strBirthDay = chaFileControl.parameter.strBirthDay,
						lifestyle = chaFileControl.gameinfo.lifestyle,
						pheromone = chaFileControl.gameinfo.flavorState[0],
						reliability = chaFileControl.gameinfo.flavorState[1],
						reason = chaFileControl.gameinfo.flavorState[2],
						instinct = chaFileControl.gameinfo.flavorState[3],
						dirty = chaFileControl.gameinfo.flavorState[4],
						wariness = chaFileControl.gameinfo.flavorState[5],
						darkness = chaFileControl.gameinfo.flavorState[6],
						sociability = chaFileControl.gameinfo.flavorState[7],
						skill_n01 = chaFileControl.gameinfo.normalSkill[0],
						skill_n02 = chaFileControl.gameinfo.normalSkill[1],
						skill_n03 = chaFileControl.gameinfo.normalSkill[2],
						skill_n04 = chaFileControl.gameinfo.normalSkill[3],
						skill_n05 = chaFileControl.gameinfo.normalSkill[4],
						skill_h01 = chaFileControl.gameinfo.hSkill[0],
						skill_h02 = chaFileControl.gameinfo.hSkill[1],
						skill_h03 = chaFileControl.gameinfo.hSkill[2],
						skill_h04 = chaFileControl.gameinfo.hSkill[3],
						skill_h05 = chaFileControl.gameinfo.hSkill[4],
						wish_01 = chaFileControl.parameter.wish01,
						wish_02 = chaFileControl.parameter.wish02,
						wish_03 = chaFileControl.parameter.wish03,
						sex = (int)chaFileControl.parameter.sex,
						FullPath = folderAssist.lstFile[i].FullPath,
						FileName = folderAssist.lstFile[i].FileName,
						time = folderAssist.lstFile[i].time,
						gameRegistration = chaFileControl.gameinfo.gameRegistration,
						flavorState = new Dictionary<int, int>(chaFileControl.gameinfo.flavorState),
						phase = chaFileControl.gameinfo.phase,
						normalSkill = new Dictionary<int, int>(chaFileControl.gameinfo.normalSkill),
						hSkill = new Dictionary<int, int>(chaFileControl.gameinfo.hSkill),
						favoritePlace = chaFileControl.gameinfo.favoritePlace,
						futanari = chaFileControl.parameter.futanari,
						cateKind = (charaCategoryKind | charaCategoryKind2),
						data_uuid = chaFileControl.dataID,
						isInSaveData = list.Contains(Path.GetFileNameWithoutExtension(chaFileControl.charaFileName))
					});
				}

			}

			UnityEngine.Resources.UnloadUnusedAssets();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, false);

			return false;//stop the original code from running
		}

#endif


		private static void InitFusion(CvsO_Fusion __instance, Button ___btnFusion,
			CustomCharaWindow ___charaLoadWinA, CustomCharaWindow ___charaLoadWinB)
		{
			_charaFusion = __instance;
			_charaFusionVisible = __instance.GetComponentsInParent<CanvasGroup>(true);

			// Fix fusion button not working when cards from different folers are used
			___btnFusion.onClick.RemoveAllListeners();
			___btnFusion.onClick.AddListener(() =>
			{
				var info = ___charaLoadWinA.GetSelectInfo();
				var info2 = ___charaLoadWinB.GetSelectInfo();
				__instance.FusionProc(info.info.FullPath, info2.info.FullPath);
				__instance.isFusion = true;
			});
		}


	}
}