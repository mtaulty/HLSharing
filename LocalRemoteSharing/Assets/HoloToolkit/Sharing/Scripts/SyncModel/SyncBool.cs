#define MIKET_CHANGE
//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
//

using System.ComponentModel;

namespace HoloToolkit.Sharing.SyncModel
{
  /// <summary>
  /// This class implements the boolean primitive for the syncing system.
  /// It does the heavy lifting to make adding new bools to a class easy.
  /// </summary>
#if MIKET_CHANGE
  public class SyncBool : SyncPrimitive, INotifyPropertyChanged
#else
    public class SyncBool : SyncPrimitive
#endif
  {
    private BoolElement element;
    private bool value;

#if MIKET_CHANGE
    public event PropertyChangedEventHandler PropertyChanged;
#endif


#if UNITY_EDITOR
    public override object RawValue
    {
      get { return value; }
    }
#endif

    public bool Value
    {
      get { return value; }

      set
      {
        // Has the value actually changed?
        if (this.value != value)
        {
          // Change the value
          this.value = value;

          if (element != null)
          {
            // Notify network that the value has changed
            element.SetValue(value);
          }
#if MIKET_CHANGE
          FirePropertyChanged();
#endif
        }
      }
    }

    public SyncBool(string field) : base(field) { }

    public override void InitializeLocal(ObjectElement parentElement)
    {
      element = parentElement.CreateBoolElement(XStringFieldName, value);
      NetworkElement = element;
    }

    public void AddFromLocal(ObjectElement parentElement, bool localValue)
    {
      InitializeLocal(parentElement);
      Value = localValue;
    }

    public override void AddFromRemote(Element remoteElement)
    {
      NetworkElement = remoteElement;
      element = BoolElement.Cast(remoteElement);
      value = element.GetValue();
    }

    public override void UpdateFromRemote(bool remoteValue)
    {
      value = remoteValue;

#if MIKET_CHANGE
      FirePropertyChanged();
#endif
    }
#if MIKET_CHANGE
    void FirePropertyChanged(string property = "Value")
    {
      var handlers = this.PropertyChanged;
      if (handlers != null)
      {
        handlers(this, new PropertyChangedEventArgs(property));
      }
    }
#endif
  }
}