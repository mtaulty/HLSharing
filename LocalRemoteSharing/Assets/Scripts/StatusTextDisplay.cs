using HoloToolkit.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatusTextDisplay : Singleton<StatusTextDisplay>
{
  public void SetStatusText(string text)
  {
    this.gameObject.GetComponent<TextMesh>().text = text;
  }
  public void ClearStatusText()
  {
    this.SetStatusText(string.Empty);
  }
}
