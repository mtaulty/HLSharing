using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;
using System;
using UnityEngine;
using UnityEngine.Events;

public class UserMoveable : MonoBehaviour, IManipulationHandler, IInputClickHandler
{
  [SerializeField]
  TextToSpeechManager textToSpeechManager;

  [SerializeField]
  bool isResponsibleForLocking;

  public event EventHandler Locked;

  enum Rail
  {
    X,
    Y
  }
  private void OnEnable()
  {
    if (this.isResponsibleForLocking)
    {
      this.textToSpeechManager.SpeakText(
        "Tap to toggle the model following you and drag to raise or rotate. Say lock when done");
    }
  }
  public void OnLock()
  {
    // We're done.
    this.gameObject.GetComponent<KeywordManager>().StopKeywordRecognizer();

    if (this.Locked != null)
    {
      this.Locked(this, EventArgs.Empty);
    }
  }
  public UserMoveable()
  {
    this.rail = Rail.X;
  }
  public void OnManipulationStarted(ManipulationEventData eventData)
  {
    if (!this.isLockedToGaze)
    {
      this.lastDelta = eventData.CumulativeDelta;
    }
  }
  public void OnManipulationUpdated(ManipulationEventData eventData)
  {
    if (!this.isLockedToGaze)
    {
      if (this.rail == null)
      {
        if (Math.Abs(eventData.CumulativeDelta.x) > Math.Abs(eventData.CumulativeDelta.y))
        {
          this.rail = Rail.X;
        }
        else
        {
          this.rail = Rail.Y;
        }
      }
      // Strangely, this can happen
      if (this.lastDelta.HasValue)
      {
        var delta = eventData.CumulativeDelta - this.lastDelta.Value;

        var xDelta = (0 - delta.x) * HORIZONTAL_FACTOR;

        if (this.rail == Rail.X)
        {
          this.gameObject.transform.Rotate(0, xDelta, 0, Space.Self);
        }
        else
        {
          this.gameObject.transform.Translate(0, delta.y * VERTICAL_FACTOR, 0, Space.World);
        }
      }
      this.lastDelta = eventData.CumulativeDelta;
    }
  }
  public void OnManipulationCompleted(ManipulationEventData eventData)
  {
    this.Done();
  }
  public void OnManipulationCanceled(ManipulationEventData eventData)
  {
    this.Done();
  }
  void Done()
  {
    this.lastDelta = null;
    this.rail = null;
  }
  public void OnInputClicked(InputClickedEventData eventData)
  {
    this.isLockedToGaze = !this.isLockedToGaze;

    if (this.isLockedToGaze)
    {
      this.gazeLockedDistance =
        Math.Max(
          Vector3.Distance(this.gameObject.transform.position, GazeManager.Instance.GazeOrigin),
          MIN_GAZE_LOCK_DISTANCE);

      this.centreOffset = GazeManager.Instance.GazeOrigin +
        (this.gazeLockedDistance * GazeManager.Instance.GazeNormal) - this.gameObject.transform.position;
    }
  }
  void Update()
  {
    if (this.isLockedToGaze)
    {
      var gazeOrigin = GazeManager.Instance.GazeOrigin;

      var gazePosition = gazeOrigin + (GazeManager.Instance.GazeNormal * this.gazeLockedDistance);

      this.gameObject.transform.Translate(
        gazePosition.x - this.gameObject.transform.position.x - this.centreOffset.x,
        0,
        gazePosition.z - this.gameObject.transform.position.z - this.centreOffset.z,
        Space.World);
    }
  }
  bool isLockedToGaze;
  Rail? rail;
  Vector3? lastDelta;
  Vector3 centreOffset;
  float gazeLockedDistance;

  // These are all really just fudge factors based on a small set of observations.
  const float HORIZONTAL_FACTOR = 250.0f;
  const float VERTICAL_FACTOR = 2.5f;
  const float MIN_GAZE_LOCK_DISTANCE = 0.5f;
}