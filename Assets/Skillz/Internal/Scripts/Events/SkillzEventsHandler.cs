using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SkillzSDK;

namespace SkillzSDK
{
  public class SkillzEventsHandler : MonoBehaviour
  {

    private void OnEnable()
    {
      SkillzEvents.OnMatchWillBegin += OnMatchWillBegin;
      SkillzEvents.OnSkillzWillExit += OnSkillzWillExit;
      SkillzEvents.OnProgressionRoomEnter += OnProgressionRoomEnter;
      SkillzEvents.OnTutorialScreenEnter += OnTutorialScreenEnter;
      SkillzEvents.OnEventReceived += OnEventReceived;
      SkillzEvents.OnNPUConversion += OnNPUConversion;
      SkillzEvents.OnReceivedMemoryWarning += OnReceivedMemoryWarning;
    }

    private void OnDisable()
    {
      SkillzEvents.OnMatchWillBegin -= OnMatchWillBegin;
      SkillzEvents.OnSkillzWillExit -= OnSkillzWillExit;
      SkillzEvents.OnProgressionRoomEnter -= OnProgressionRoomEnter;
      SkillzEvents.OnTutorialScreenEnter -= OnTutorialScreenEnter;
      SkillzEvents.OnEventReceived -= OnEventReceived;
      SkillzEvents.OnNPUConversion -= OnNPUConversion;
      SkillzEvents.OnReceivedMemoryWarning -= OnReceivedMemoryWarning;
    }

    protected virtual void OnMatchWillBegin(Match match) { }
    protected virtual void OnSkillzWillExit() { }
    protected virtual void OnProgressionRoomEnter() { }
    protected virtual void OnTutorialScreenEnter() { }
    protected virtual void OnEventReceived(string eventName, Dictionary<string, string> eventData) { }
    protected virtual void OnNPUConversion() { }
    protected virtual void OnReceivedMemoryWarning() { }
  }
}
