﻿using GoogleARCore;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class CameraImageManager : MonoBehaviour
{
    // Where to send the images, could be public for inspector or read from .env
    private string url = "http://34.83.254.195/detect";
    //private string url = "https://httpbin.org/post";

    private string lastMessageReceived = "";
    public TMP_Text text;

    public GameObject UI;
    public GameObject Planes;
    public TargetSpawner targetSpawner;
    public GameObject PointCloud;

    private DetectionFacts detectionFacts;


    public void createScreenshot()
    {
        // Async screenshots are not supported, this was to test that..
        //StartCoroutine(AsyncScreenshot());

        StartCoroutine(CreateScrenshotWithoutUI());

        //lastPosition = Camera.main.transform.position;
        //var tex = ScreenCapture.CaptureScreenshotAsTexture();
        //var bytes = tex.EncodeToPNG();
        //var base64 = Convert.ToBase64String(bytes);
        //StartCoroutine(SendPostRequest(base64));
    }

    //private RenderTexture renderTexture;

    //IEnumerator TextureScreenshot()
    //{
    //    yield return new WaitForEndOfFrame();

    //    renderTexture = new RenderTexture(Screen.width/4, Screen.height/4, 0);
    //    ScreenCapture.CaptureScreenshotIntoRenderTexture(renderTexture);
    //    AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32, ReadbackCompleted);
    //}

    //void ReadbackCompleted(AsyncGPUReadbackRequest request)
    //{
    //    // Render texture no longer needed, it has been read back.
    //    DestroyImmediate(renderTexture);

    //    using (var imageBytes = request.GetData<byte>())
    //    {
    //        var base64 = Convert.ToBase64String(imageBytes.ToArray());
    //        StartCoroutine(SendPostRequest(base64));
    //    }
    //}

    /// <summary>
    /// Hide UI, take screenshot, store camera data, show UI.
    /// </summary>
    /// <returns></returns>
    public IEnumerator CreateScrenshotWithoutUI()
    {
        UI.SetActive(false);
        Planes.SetActive(false);
        targetSpawner.SetVisibleMeshes(false);
        PointCloud.SetActive(false);

        yield return null;

        detectionFacts = new DetectionFacts();
        detectionFacts.pixelWidth = Camera.main.pixelWidth;
        detectionFacts.pixelHeight = Camera.main.pixelHeight;
        detectionFacts.nearClipPlane = Camera.main.nearClipPlane;
        detectionFacts.worldToScreen = Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix;

        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        var resized = Resize(tex, tex.width / 4, tex.height / 4);
        //tex.Resize(tex.width / 4, tex.height / 4);
        //tex.Apply();

        var bytes = resized.EncodeToPNG();
        //var base64 = Convert.ToBase64String(bytes);
        StartCoroutine(SendPostRequestBytes(bytes));

        yield return null;
        UI.SetActive(true);
        Planes.SetActive(true);
        targetSpawner.SetVisibleMeshes(true);
        PointCloud.SetActive(true);

        MainUIController.Instance._resetButton.SetActive(true);
    }

    /// <summary>
    /// Send to server and wait for response
    /// </summary>
    /// <param name="imgBytes"></param>
    /// <returns></returns>
    public IEnumerator SendPostRequestBytes(byte[] imgBytes)
    {
        text.text = "Sending Request...";
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();

        formData.Add(new MultipartFormFileSection("image", imgBytes, "image.png", "image/png"));

        UnityWebRequest www = UnityWebRequest.Post(url, formData);
        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
            // Text could be used in a more sensible UI
            text.text = www.error;
        }
        else
        {
            Debug.Log("Form upload complete!");
            text.text = "Form upload complete!";
        }

        while (!www.downloadHandler.isDone)
        {
            yield return null;
        }

        lastMessageReceived = www.downloadHandler.text;
        text.text = "Received: " + lastMessageReceived;

        // Assume we receive the correct JSON
        var json = Newtonsoft.Json.JsonConvert.DeserializeObject<DetectionResponse>(lastMessageReceived);
        if (!json.points.tracks)
        {
            text.text = "No tracks found";
            MainUIController.Instance.ActivateUserButtons(false);
        }
        else
        {
            text.text = "Tracks found!";

            // Right now we to the camera calculations here

            // Read points and enlarge back to original screen size
            var Left1 = new Vector3(json.points.left[0][0] * 4, detectionFacts.pixelHeight - json.points.left[0][1] * 4, 0);
            var Left2 = new Vector3(json.points.left[1][0] * 4, detectionFacts.pixelHeight - json.points.left[1][1] * 4, 0);
            var Right1 = new Vector3(json.points.right[0][0] * 4, detectionFacts.pixelHeight - json.points.right[0][1] * 4, 0);
            var Right2 = new Vector3(json.points.right[1][0] * 4, detectionFacts.pixelHeight - json.points.right[1][1] * 4, 0);

            // For all 4 points, use the 
            var l1 = targetSpawner.CreateTrackAnchorRaycast(ScreenPointToRay(Left1));
            var l2 = targetSpawner.CreateTrackAnchorRaycast(ScreenPointToRay(Left2));
            Debug.DrawLine(l1.position, l2.position, Color.green, 20f);

            var r1 = targetSpawner.CreateTrackAnchorRaycast(ScreenPointToRay(Right1));
            var r2 = targetSpawner.CreateTrackAnchorRaycast(ScreenPointToRay(Right2));
            Debug.DrawLine(r1.position, r2.position, Color.red, 20f);

            if(l1 != null && l2 != null && r1 != null && r2 != null)
            {
                var P1 = (l1.position + l2.position) / 2f;
                var P2 = GetClosestPointOnFiniteLine(P1, r1.position, r2.position);
                targetSpawner.ShowClearance(P1, P2);
            }

            

            MainUIController.Instance.ActivateUserButtons(true);
            MainUIController.Instance._resetButton.SetActive(true);
        }
    }


    public static Vector3 GetClosestPointOnFiniteLine(Vector3 point, Vector3 line_start, Vector3 line_end)
    {
        Vector3 line_direction = line_end - line_start;
        float line_length = line_direction.magnitude;
        line_direction.Normalize();
        float project_length = Mathf.Clamp(Vector3.Dot(point - line_start, line_direction), 0f, line_length);
        return line_start + line_direction * project_length;
    }

    public static Vector3 GetClosestPointOnInfiniteLine(Vector3 point, Vector3 line_start, Vector3 line_end)
    {
        return line_start + Vector3.Project(point - line_start, line_end - line_start);
    }

    /// <summary>
    /// Uses current detection facts to determine the previous screel to world transformation.
    /// </summary>
    /// <param name="sp"></param>
    /// <returns></returns>
    public Ray ScreenPointToRay(Vector3 sp)
    {
        // We create 2 vector to create the ray. First on at the camera plane, second one 1m in front.
        var v0 = manualScreenPointToWorld(detectionFacts.worldToScreen, detectionFacts.pixelWidth,
                detectionFacts.pixelHeight, 0, sp);
        var v1 = manualScreenPointToWorld(detectionFacts.worldToScreen, detectionFacts.pixelWidth,
                detectionFacts.pixelHeight, 1, sp);

        // Create Ray
        var ray = new Ray(v0, (v1-v0).normalized);
        return ray;
    }

    /// <summary>
    /// Uses inverse of old world2screen to determine where the point was previously in screen space when the request was sent.
    /// </summary>
    /// <param name="world2Screen"></param>
    /// <param name="pixelWidth"></param>
    /// <param name="pixelHeight"></param>
    /// <param name="nearClipPlane"></param>
    /// <param name="sp"></param>
    /// <returns></returns>
    Vector3 manualScreenPointToWorld(Matrix4x4 world2Screen, int pixelWidth, int pixelHeight, float nearClipPlane, Vector3 sp)
    {
        Matrix4x4 screen2World = world2Screen.inverse;

        float[] inn = new float[4];

        inn[0] = 2.0f * (sp.x / pixelWidth) - 1.0f;
        inn[1] = 2.0f * (sp.y / pixelHeight) - 1.0f;
        inn[2] = nearClipPlane;
        inn[3] = 1.0f;

        Vector4 pos = screen2World * new Vector4(inn[0], inn[1], inn[2], inn[3]);

        pos.w = 1.0f / pos.w;

        pos.x *= pos.w;
        pos.y *= pos.w;
        pos.z *= pos.w;

        return new Vector3(pos.x, pos.y, pos.z);
    }

    /// <summary>
    /// Resize an image and draw it in a new texture.
    /// </summary>
    /// <param name="texture2D">Input image</param>
    /// <param name="targetX"></param>
    /// <param name="targetY"></param>
    /// <returns></returns>
    Texture2D Resize(Texture2D texture2D, int targetX, int targetY)
    {
        RenderTexture rt = new RenderTexture(targetX, targetY, 24);
        RenderTexture.active = rt;
        Graphics.Blit(texture2D, rt);
        Texture2D result = new Texture2D(targetX, targetY);

        // Read from active RT
        result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
        result.Apply();
        return result;
    }

    public IEnumerator SendPostRequest(string img64)
    {
        text.text = "Sending Request...";
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormDataSection("image64=" + img64));
        //formData.Add(new MultipartFormDataSection("test=" + "test123"));
        //formData.Add(new MultipartFormFileSection("my file data", "myfile.txt"));

        UnityWebRequest www = UnityWebRequest.Post(url, formData);
        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
            text.text = www.error;
        }
        else
        {
            Debug.Log("Form upload complete!");
            text.text = "Form upload complete!";
        }

        while (!www.downloadHandler.isDone)
        {
            yield return null;
        }

        lastMessageReceived = www.downloadHandler.text;
        text.text = "Received: " + lastMessageReceived.Length;

    }

    public class DetectionResponse
    {
        public Points points;
    }

    public class Points
    {
        public bool tracks;

        public int[][] left;
        public int[][] right;
    }

    public class DetectionFacts
    {
        public int imgWidth;
        public int imgHeight;

        public int pixelWidth;
        public int pixelHeight;
        public float nearClipPlane;

        public Matrix4x4 worldToScreen;

        public Vector3 cameraPosition;
        public Quaternion cameraRotation;

    }
}
