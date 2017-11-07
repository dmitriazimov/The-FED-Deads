using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Timeline;

class MenuSystem : MonoBehaviour {

    #region References
    GameObject menuCam;
    Slider volumeSlider;
    Dropdown langSelect;
    GameObject translationManager;
    AudioSource [] menuMusic;
    GameObject playerToTunnel;
    #endregion

    #region Transition to Game
    const float cutSceneDuration = 7f;
    const int fps = 60;
    float audioDampInterval;
    #region

    void Awake ()
    {
        Init();
	}

    void Init()
    {
        InitTiming();
        InitAudio();
        InitLanguage();
        InitMisc();
        InitPlayer();
    }
	void InitTiming()
    { // Important to avoid timescale bugs when coming back from main game
        Time.timeScale = 1;
    }

    void InitMisc()
    {
        Application.targetFrameRate = 60; // Limiting the scene to 60 fps for the fadeout and audiodamp to work on proper timing
        menuCam = Camera.main.gameObject;
    }

    void InitPlayer()
    { // Getting a reference on the character that would enter a timeline at the to game transition
        playerToTunnel = GameObject.Find("soldier_LOD0 (6)");
        playerToTunnel.GetComponent<MenuSoldier>().enabled = true; // In case it was deactivated by a previous transition
        playerToTunnel.GetComponent<MenuSoldier>().ActivateMuselFire(); // In case it was deactivated by a previous transition
    }
    void InitLanguage()
    { // Interrogating the language manager on the current language and setting the dropdown to the appropriate option
        langSelect = GameObject.Find("LangSelect").GetComponent<Dropdown>();
        translationManager = GameObject.Find("TranslationManager");
        if (Application.systemLanguage == SystemLanguage.English)
        {
            langSelect.value = 0;
        }
        if (Application.systemLanguage == SystemLanguage.French)
        {
            langSelect.value = 1;
        }
        // Finding out if the player has previously changed the language preferences and using the new preference to override the old one
        if (PlayerPrefs.HasKey("ChosenLanguage"))
        {
            if(PlayerPrefs.GetString("ChosenLanguage") == "English")
            {
                langSelect.value = 0;
            }
            if (PlayerPrefs.GetString("ChosenLanguage") == "French")
            {
                langSelect.value = 1;
            }
        }
        langSelect.onValueChanged.AddListener(delegate { LangSelectCheck(); }); // Delegating a language check to the dropdown
        LangSelectCheck(); // Performing initial language check
    }

    void InitAudio()
    { // Getting a reference to the volume slider and delegating a value check to listen to any changes
        volumeSlider = GameObject.Find("VolumeSlider").GetComponent<Slider>();
        volumeSlider.onValueChanged.AddListener(delegate { ValueChangeCheck(); });

        // Retrieving the available menu musics and playing one at random
        menuMusic = gameObject.GetComponents<AudioSource>();
        menuMusic[Random.Range(0, 2)].Play();

        // Checking if the player has previously defined a desired volume and adjusting the slider and master volume accordingly
        if (PlayerPrefs.HasKey("MasterVolume") == false)
        {
            PlayerPrefs.SetFloat("MasterVolume", 0.50f);
        }
        volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume");
        AudioListener.volume = volumeSlider.value;

        // Progressive damping of the volume at the Play transition
        audioDampInterval = PlayerPrefs.GetFloat("MasterVolume") / (fps * cutSceneDuration);
    }

    // Requesting camera transitions when button are pressed
    public void PlayButton()
    {
        menuCam.GetComponent<CameraModes>().TransitionCamera("FromMainToPlay");
        // Performing the player going to the sewer timeline with the audiodamp and the fadeout
        StartCoroutine(LoadGame());
        StartCoroutine(DampAudio());
        playerToTunnel.GetComponent<MenuSoldier>().DeactivateMuselFire();
        playerToTunnel.GetComponent<MenuSoldier>().enabled = false;
        playerToTunnel.GetComponent<PlayableDirector>().Play();
    }
    public void BackFromSettingsButton()
    {
        menuCam.GetComponent<CameraModes>().TransitionCamera("FromSettingsToMain");
    }
    public void BackFromEULAButton()
    {
        menuCam.GetComponent<CameraModes>().TransitionCamera("FromEULAToMain");
    }
    public void BackFromCreditsButton()
    {
        menuCam.GetComponent<CameraModes>().TransitionCamera("FromCreditsToMain");
    }
    public void OptionsButton()
    {
        menuCam.GetComponent<CameraModes>().TransitionCamera("FromMainToSettings");
    }
    public void CreditsButton()
    {
        menuCam.GetComponent<CameraModes>().TransitionCamera("FromMainToCredits");
    }
    public void EULAButton()
    {
        menuCam.GetComponent<CameraModes>().TransitionCamera("FromMainToEULA");
    }
    public void QuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
Application.Quit();
#endif
    }
    
    public void ValueChangeCheck()
    { // Saving the new volume value when the player moves the slider
        PlayerPrefs.SetFloat("MasterVolume", volumeSlider.value);
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume");
        audioDampInterval = PlayerPrefs.GetFloat("MasterVolume") / (fps * cutSceneDuration);
    }

    public void LangSelectCheck()
    { // Saving the new language preference when the player changed the dropdow option
        if(langSelect.value == 0)
        {
            translationManager.GetComponent<TranslationManager>().SetLanguage("English");
            PlayerPrefs.SetString("ChosenLanguage", "English");
        }
        if (langSelect.value == 1)
        {
            translationManager.GetComponent<TranslationManager>().SetLanguage("French");
            PlayerPrefs.SetString("ChosenLanguage", "French");
        }
    }

    IEnumerator LoadGame()
    {
        yield return new WaitForSeconds(cutSceneDuration);
        SceneManager.LoadScene("GameLevel1");
    }

    IEnumerator DampAudio()
    {
        while (AudioListener.volume >= 0)
        {
            AudioListener.volume -= audioDampInterval;
            yield return null;
        }
    }
}
