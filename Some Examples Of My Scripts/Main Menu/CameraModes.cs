using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/* This system enables the camera to:
 * 1. Use timeline to transition between menu sections
 * 2. Slowly move the camera while it's position is constant for a "wobbly effect"
 * 3. Fadout to the logo when Play is pressed
 */

class CameraModes : MonoBehaviour {

    #region Camera Transforms
    Transform tanker;
	Transform ground;
	Transform tank;
	Transform window;
	Transform current;
    #endregion

    #region Playable Directors
    PlayableDirector fromMainToSettings;
	PlayableDirector fromSettingsToMain;
	PlayableDirector fromMainToEULA;
	PlayableDirector fromEULAToMain;
	PlayableDirector fromMainToCredits;
	PlayableDirector fromCreditsToMain;
    PlayableDirector fromMainToPlay;
    #endregion

    #region Transition Timing
    float timer;
    const float normalTransitionRate = 1f;
    const float toPlayTransitionRate = 7f;
    #endregion

    #region Fadeout
    const int fps = 60;
    const float alphaChangeInterval = 1 / (fps * toPlayTransitionRate);
    [SerializeField] Texture blackOut;
    [Range(0.0f, 1.0f)] public float alpha;
    #endregion

    #region Camera Wobble
    const float wobbleRate = 0.25f;
    const float maxWobbleAngle = 3f;
    bool transferInProgress;
    bool needsRepositioning;
    Vector3 wobbleDirection;
    #endregion

    void Awake ()
    {
        Init();
	}

    void Start()
    { // The wobble direction should change every few seconds
        InvokeRepeating("RandomizeWobbleDirection", 0.0f, 5.0f);
    }
	void FixedUpdate ()
    {
        UpdateMode();
	}

    void Init()
    {
        // Initializing possible transforms
		tanker = GameObject.Find("CamPosTanker").transform;
		ground = GameObject.Find("CamPosGround").transform;
		tank = GameObject.Find("CamPosTank").transform;
		window = GameObject.Find("CamPosWindow").transform;

        // Initializing playable directors for transitioning
		fromMainToSettings = GameObject.Find ("FromMainToSettings").GetComponent<PlayableDirector>();
		fromSettingsToMain = GameObject.Find ("FromSettingsToMain").GetComponent<PlayableDirector>();
		fromMainToEULA = GameObject.Find ("FromMainToEULA").GetComponent<PlayableDirector>();
		fromEULAToMain = GameObject.Find ("FromEULAToMain").GetComponent<PlayableDirector>();
		fromMainToCredits = GameObject.Find ("FromMainToCredits").GetComponent<PlayableDirector>();
		fromCreditsToMain = GameObject.Find ("FromCreditsToMain").GetComponent<PlayableDirector>();
        fromMainToPlay = GameObject.Find("FromMainToPlay").GetComponent<PlayableDirector>();

        // Initializing camera to main menu
        current = tanker;
		gameObject.transform.position = current.position;
		gameObject.transform.rotation = current.rotation;
		timer = 0f;
        alpha = 0f;
    }

    // This funciton is public to be called from the MenuSystem
    // When a button is pressed, the MenuSystem sends a transition request
	public void TransitionCamera(string transition)
	{
		switch (transition)
		{
			case "FromMainToSettings":
			{
				fromMainToSettings.Play();
                current = tank;
				timer = normalTransitionRate;
				break;
			}
			case "FromSettingsToMain":
			{
				fromSettingsToMain.Play();
                current = tanker;
				timer = normalTransitionRate;
				break;
			}
			case "FromMainToCredits":
			{
				fromMainToCredits.Play();
                current = window;
				timer = normalTransitionRate;
				break;
			}
			case "FromCreditsToMain":
			{
				fromCreditsToMain.Play();
                current = tanker;
                timer = normalTransitionRate;
				break;
			}
			case "FromMainToEULA":
			{
				fromMainToEULA.Play();
                current = ground;
				timer = normalTransitionRate;
				break;
			}
			case "FromEULAToMain":
			{
				fromEULAToMain.Play();
                current = tanker;
                timer = normalTransitionRate;
				break;
			}
            case "FromMainToPlay":
            {
                fromMainToPlay.Play();
                timer = toPlayTransitionRate;
                StartCoroutine(FadeOut());
                break;
            }
			default:
			{
				Debug.LogError ("Invalid Transition Request");
				break;
			}
		}
	}
    
	bool Transition()
	{ // Checks if the camera is transitioning by taking into account that all transitions (except for Play) take the same ammount of time
		timer -= Time.fixedDeltaTime;
		if (timer < 0f) 
		{
			timer = 0f;
			return false;
		}
		return true;
	}

    void UpdateMode()
    {
		if(!Transition())
        { // Has to be done at each frame
            SlowlyWobbleCamera();
        }
    }

    IEnumerator FadeOut()
    {
        while(alpha <= 1f)
        { // The fadeout is done by augmenting the alpha of the logo over time
            alpha += alphaChangeInterval;
            yield return new WaitForSeconds(alphaChangeInterval);
        }
    }

    void OnGUI()
    { // To display the logo
        Color newColor = GUI.color;
        newColor.a = alpha;
        GUI.color = newColor;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackOut);
    }

    void RandomizeWobbleDirection()
    {
        wobbleDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
    }

    void SlowlyWobbleCamera()
    {
        if(Quaternion.Angle(current.rotation, gameObject.transform.rotation) >= maxWobbleAngle)
        { // The camera wobble is limited to a maximum angle to avoid the camera wandering away from the buttons
            needsRepositioning = true;
        }
        if(gameObject.transform.rotation == current.rotation)
        {
            needsRepositioning = false;
        }
        if (!needsRepositioning)
        {
            gameObject.transform.Rotate(wobbleDirection, Time.deltaTime * wobbleRate);
        }
        if (needsRepositioning)
        { // Adjusting the camera angles if the critical angel has been reached
            gameObject.transform.rotation = Quaternion.RotateTowards(gameObject.transform.rotation, current.rotation, Time.deltaTime * wobbleRate);
        }
    }
}
