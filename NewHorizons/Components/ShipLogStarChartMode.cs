﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using NewHorizons.Utility;
using Logger = NewHorizons.Utility.Logger;
using UnityEngine.UI;
using OWML.Common;

namespace NewHorizons.Components
{
    public class ShipLogStarChartMode : ShipLogMode
    {
        private List<GameObject> _starSystemCards = new List<GameObject>();
        private GameObject _cardTemplate = null;
        private int _cardIndex = 0;
        private OWAudioSource _oneShotSource;

        private float _startPanTime;
        private float _panDuration;
        private Transform root;
        private Vector2 _panRootPos = Vector2.zero;
        private Vector2 _startPanPos;

        private ScreenPromptList _upperRightPromptList;
        private ScreenPromptList _centerPromptList;

        private ScreenPrompt _detectiveModePrompt;
        private ScreenPrompt _targetSystemPrompt;

        private ShipLogEntryCard _target = null;
        private NotificationData _warpNotificationData = null;

        public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
        {
            root = base.transform.Find("ScaleRoot/PanRoot");
            _oneShotSource = oneShotSource;

            _centerPromptList = centerPromptList;
            _upperRightPromptList = upperRightPromptList;

            _detectiveModePrompt = new ScreenPrompt(InputLibrary.swapShipLogMode, "Rumor Mode", 0, ScreenPrompt.DisplayState.Normal, false);
            _targetSystemPrompt = new ScreenPrompt(InputLibrary.markEntryOnHUD, "Target", 0, ScreenPrompt.DisplayState.Normal, false);

            GlobalMessenger<ReferenceFrame>.AddListener("TargetReferenceFrame", new Callback<ReferenceFrame>(OnTargetReferenceFrame));
            GlobalMessenger<OWRigidbody>.AddListener("EnterFlightConsole", new Callback<OWRigidbody>(OnEnterFlightConsole));

            var x = 0;
            foreach (var starSystem in Main.BodyDict.Keys)
            {
                // Conditions to allow warping into that system (either no planets (stock system) or has a ship spawn point)
                var flag = false;
                if (starSystem.Equals("SolarSystem")) flag = true;
                else
                {
                    foreach(var body in Main.BodyDict[starSystem])
                    {
                        if(body.Config?.Spawn?.ShipSpawnPoint != null)
                        {
                            flag = true;
                            break;
                        }
                    }
                }

                if(flag)
                {
                    var card = CreateCard(starSystem, root.transform, new Vector2(x++ * 200, 0));
                    _starSystemCards.Add(card);
                }
            }
        }

        public void OnDestroy()
        {
            GlobalMessenger<ReferenceFrame>.RemoveListener("TargetReferenceFrame", new Callback<ReferenceFrame>(OnTargetReferenceFrame));
            GlobalMessenger<OWRigidbody>.RemoveListener("EnterFlightConsole", new Callback<OWRigidbody>(OnEnterFlightConsole));
        }

        private void OnEnterFlightConsole(OWRigidbody _)
        {
            if (_target == null) GlobalMessenger.FireEvent("UntargetReferenceFrame");
        }

        public GameObject CreateCard(string uniqueName, Transform parent, Vector2 position)
        {
            if (_cardTemplate == null)
            {
                var panRoot = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/DetectiveMode/ScaleRoot/PanRoot");
                _cardTemplate = GameObject.Instantiate(panRoot.GetComponentInChildren<ShipLogEntryCard>().gameObject);
                _cardTemplate.SetActive(false);
            }

            var newCard = GameObject.Instantiate(_cardTemplate, parent);
            newCard.transform.Find("EntryCardRoot/NameBackground/Name").GetComponent<UnityEngine.UI.Text>().text = UniqueNameToString(uniqueName);
            newCard.SetActive(true);
            newCard.transform.name = uniqueName;
            newCard.transform.localPosition = new Vector3(position.x, position.y, 40);
            newCard.transform.localRotation = Quaternion.Euler(0, 0, 0);

            var shipLogEntryCard = newCard.GetComponent<ShipLogEntryCard>();

            Texture texture = null;
            try
            {
                if (uniqueName.Equals("SolarSystem"))
                {
                    IModAssets assets = Main.Instance.ModHelper.Assets;
                    texture = assets.GetTexture("AssetBundle/hearthian system.png");
                }
                else
                {
                    IModAssets assets = Main.BodyDict[uniqueName][0].Mod.Assets;
                    var path = $"planets/{uniqueName}.png";
                    Logger.Log($"Trying to load {path}");
                    texture = assets.GetTexture(path);
                }
            }
            catch (Exception) { }

            if(texture != null)
            {
                shipLogEntryCard._photo.sprite = MakeSprite((Texture2D)texture);
                newCard.transform.Find("EntryCardRoot/EntryCardBackground/PhotoImage").gameObject.SetActive(true);
            }

            shipLogEntryCard._hudMarkerIcon.gameObject.SetActive(false);
            shipLogEntryCard._moreToExploreIcon.gameObject.SetActive(false);
            shipLogEntryCard._unreadIcon.gameObject.SetActive(false);

            return newCard;
        }

        public override bool AllowCancelInput()
        {
            return true;
        }

        public override bool AllowModeSwap()
        {
            return true;
        }

        public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
        {
            base.gameObject.SetActive(true);

            Locator.GetPromptManager().AddScreenPrompt(_detectiveModePrompt, _upperRightPromptList, TextAnchor.MiddleRight, -1, true);
            Locator.GetPromptManager().AddScreenPrompt(_targetSystemPrompt, _centerPromptList, TextAnchor.MiddleCenter, -1, true);
        }

        public override void ExitMode()
        {
            base.gameObject.SetActive(false);

            Locator.GetPromptManager().RemoveScreenPrompt(_detectiveModePrompt);
            Locator.GetPromptManager().RemoveScreenPrompt(_targetSystemPrompt);
        }

        public override string GetFocusedEntryID()
        {
            return "";
        }

        public override void OnEnterComputer()
        {

        }

        public override void OnExitComputer()
        {

        }

        public override void UpdateMode()
        {
            UpdateMapCamera();
            UpdateMapNavigation();
            UpdatePrompts();
        }

        private void UpdateMapCamera()
        {
            Vector2 b = -_starSystemCards[_cardIndex].transform.localPosition;
            float num = Mathf.InverseLerp(_startPanTime, _startPanTime + _panDuration, Time.unscaledTime);
            num = 1f - (num - 1f) * (num - 1f);
            _panRootPos = Vector2.Lerp(_startPanPos, b, num);
            root.transform.localPosition = new Vector3(_panRootPos.x, _panRootPos.y, 0);
        }

        private void UpdateMapNavigation()
        {
            var oldIndex = _cardIndex;
            if (OWInput.IsNewlyPressed(InputLibrary.right, InputMode.All) || OWInput.IsNewlyPressed(InputLibrary.right2, InputMode.All))
            {
                _cardIndex = Posmod(_cardIndex + 1, _starSystemCards.Count());
            }
            else if (OWInput.IsNewlyPressed(InputLibrary.left, InputMode.All) || OWInput.IsNewlyPressed(InputLibrary.left2, InputMode.All))
            {
                _cardIndex = Posmod(_cardIndex - 1, _starSystemCards.Count());
            }

            if (oldIndex != _cardIndex)
            {
                Logger.Log($"Moving to {_cardIndex}");
                _oneShotSource.PlayOneShot(global::AudioType.ShipLogMoveBetweenPlanets, 1f);
                _startPanTime = Time.unscaledTime;
                _startPanPos = _panRootPos;
                _panDuration = 0.25f;
            }
        }

        private void UpdatePrompts()
        {
            if (OWInput.IsNewlyPressed(InputLibrary.markEntryOnHUD, InputMode.All))
            {
                var shipLogEntryCard = _starSystemCards[_cardIndex].GetComponent<ShipLogEntryCard>();

                if (_target == shipLogEntryCard) RemoveWarpTarget();
                else SetWarpTarget(shipLogEntryCard);
            }
        }

        public string UniqueNameToString(string uniqueName)
        {
            if (uniqueName.Equals("SolarSystem")) return "Hearthian System";

            var splitString = uniqueName.Split('.');
            if (splitString.Length > 1) splitString = splitString.Skip(1).ToArray();
            var name = string.Join("", splitString).SplitCamelCase();
            return name;
        }

        private int Posmod(int a, int b)
        {
            return (a % b + b) % b;
        }

        private Sprite MakeSprite(Texture2D texture)
        {
            var rect = new Rect(0, 0, texture.width, texture.height);
            var pivot = new Vector2(texture.width / 2, texture.height / 2);
            return Sprite.Create(texture, rect, pivot);
        }

        private void OnTargetReferenceFrame(ReferenceFrame referenceFrame)
        {
            RemoveWarpTarget();
        }

        private void SetWarpTarget(ShipLogEntryCard shipLogEntryCard)
        {
            RemoveWarpTarget(false);
            _oneShotSource.PlayOneShot(global::AudioType.ShipLogUnmarkLocation, 1f);
            _target = shipLogEntryCard;
            _target.SetMarkedOnHUD(true);
            GlobalMessenger.FireEvent("UntargetReferenceFrame");
            _warpNotificationData = new NotificationData($"AUTOPILOT LOCKED TO:\n{UniqueNameToString(shipLogEntryCard.name).ToUpper()}");
            NotificationManager.SharedInstance.PostNotification(_warpNotificationData, true);
        }

        private void RemoveWarpTarget(bool playSound = false)
        {
            if(_warpNotificationData != null) NotificationManager.SharedInstance.UnpinNotification(_warpNotificationData);
            if (_target == null) return;
            if(playSound) _oneShotSource.PlayOneShot(global::AudioType.ShipLogMarkLocation, 1f);
            _target.SetMarkedOnHUD(false);
            _target = null;
        }

        public string GetTargetStarSystem()
        {
            return _target?.name;
        }
    }
}
