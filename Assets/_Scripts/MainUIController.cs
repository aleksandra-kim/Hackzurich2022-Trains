﻿using GoogleARCore.Examples.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;


public class MainUIController : MonoBehaviour
{

    [Header("Warning Splash Screen")]
    public GameObject _warningSplashScreen;
    public float _splashScreenDuration = 2;
    public float _fadeDuration = 0.5f;

    [Header("Snackbar")]
    public GameObject _snackbarPanel;
    public TMP_Text _snackbarText;
    public string _panelAnchorText;

    [Header("User Buttons")]
    public GameObject _userButtons;
    public GameObject _resetButton;
    public GameObject _transpButton;
    public GameObject _tunnelButton;
    public GameObject _autoButton;
    public GameObject _manualButton;

    [Header("In Game Objects")]
    public GameObject _planeDiscovery;

    public static MainUIController Instance { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        StartCoroutine(FadeoutPanel());
    }

    /// <summary>
    /// Restarts the whole application
    /// </summary>
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Fade out the intro splash screen and activate the user buttons
    /// </summary>
    /// <returns></returns>
    IEnumerator FadeoutPanel()
    {
        float elapsedTime = 0;

        yield return new WaitForSeconds(_splashScreenDuration);

        var canvasGroup = _warningSplashScreen.GetComponent<CanvasGroup>();

        while (elapsedTime < _fadeDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(1, 0, elapsedTime / _fadeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        _warningSplashScreen.SetActive(false);

        _snackbarText.text = _panelAnchorText;

        _userButtons.SetActive(true);
        ActivateUserButtons(false);
        
        _resetButton.SetActive(false);
    }

    /// <summary>
    /// Enable or disable user buttons that control the tunnel graphics
    /// </summary>
    /// <param name="active">True shows the controls and hides the initial setup buttons.</param>
    public void ActivateUserButtons(bool active)
    {
        _transpButton.SetActive(active);
        _tunnelButton.SetActive(active);
        _autoButton.SetActive(!active);
        _manualButton.SetActive(!active);
    }

}
