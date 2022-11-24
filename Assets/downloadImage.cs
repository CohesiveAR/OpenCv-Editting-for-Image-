using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using UnityEngine.Networking;
using System;
using System.Threading;

public class downloadImage : MonoBehaviour
{
    [SerializeField] RawImage rawImage;
    [SerializeField] Slider sliderAlpha;
    [SerializeField] Slider sliderBeta;
    [SerializeField] Scrollbar scrollBarH;
    [SerializeField] Slider sliderR;
    [SerializeField] Slider sliderG;
    [SerializeField] Slider sliderB;
    [SerializeField] Toggle toggleIllu;
    Mat img, backUp, outputMat, mask;
    Texture2D outputTexture;  
    Thread newThread;
    bool illu = false, shouldUpdatePreview = false;
    // private System.DateTime startTime;
    enum ChangeType{
        ILLU, RGB, DENOISE
    }
    ChangeType lastChange = ChangeType.ILLU;
    void Start()
    {
        toggleIllu.onValueChanged.AddListener((v)=>{
            illu = v;
            resetUI();
        });

        StartCoroutine(DownloadImage("https://m.media-amazon.com/images/I/41RsRJYNziL.jpg"));
    }
    public void colorChange_pointerUp(){
        float r = sliderR.value, g = sliderG.value, b = sliderB.value;
        if(lastChange!=ChangeType.RGB){
            lastChange = ChangeType.RGB;
            resetUI();
            sliderR.value = r; sliderG.value = g; sliderB.value = b;
        }
        colorChangeSlider(r,g,b);
    }
    public void deNoiseChange_pointerUp(){        
        float h = scrollBarH.value;
        if(lastChange!=ChangeType.DENOISE){
            lastChange = ChangeType.DENOISE;
            resetUI();
            scrollBarH.value = h;
        }
        deNoise(h*30);
    }
    public void illuChange_pointerUp(){
        float alpha = sliderAlpha.value;
        float beta = sliderBeta.value;

        if(lastChange!=ChangeType.ILLU){
            lastChange = ChangeType.ILLU;
            resetUI();
            sliderAlpha.value = alpha;
            sliderBeta.value = beta;
        }

        if(illu){
            alpha = 2-sliderAlpha.value;
            beta = sliderBeta.value/5;
            illuminationChange(alpha, beta);
        }
        else{
            alpha = sliderAlpha.value*50;
            beta = sliderBeta.value/2;
            detailEnhance(alpha, beta);
        }
    }
    IEnumerator DownloadImage(string MediaUrl)
    {   
        // startTime = System.DateTime.UtcNow;
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(MediaUrl);
        yield return request.SendWebRequest();
        if(request.isNetworkError || request.isHttpError) 
            Debug.Log(request.error);
        else
            {
                Texture2D dwnlded = ((DownloadHandlerTexture) request.downloadHandler).texture;
                rawImage.texture = dwnlded; 
                
                img = new Mat(dwnlded.height,dwnlded.width, CvType.CV_8UC3);
                backUp = new Mat(dwnlded.height,dwnlded.width, CvType.CV_8UC3);
                outputTexture = new Texture2D(img.cols(), img.rows(), TextureFormat.RGBA32, false);
                outputMat = Mat.zeros(new Size(img.width(),img.height()),CvType.CV_8UC3);    
                mask = Mat.ones(new Size(img.width(),img.height()),CvType.CV_8UC1);  
                mask.setTo(new Scalar(255, 0, 0));

                Utils.texture2DToMat(dwnlded,img);
                img.copyTo(backUp);
            }
    }

    public void matToOutput(Mat matimg)
    {
        // System.TimeSpan ts = System.DateTime.UtcNow - startTime;

        if(true){//(ts.TotalSeconds>0.05) {
            Mat flipMat = Mat.zeros(new Size(img.width(),img.height()),CvType.CV_8UC3);  
            Core.flip(outputMat, flipMat,0);
            Core.flip(flipMat, flipMat,0);
            Utils.matToTexture2D(flipMat, outputTexture);
            rawImage.texture = outputTexture; 
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(shouldUpdatePreview){
            Debug.Log("shouldUpdatePreview");
            matToOutput(outputMat);//RestartCoroutine(matToOutput(outputMat));
            shouldUpdatePreview = false;
        }
    }

    public void resetUI(bool pressed = false){
        if(pressed){
            backUp.copyTo(img);
            img.copyTo(outputMat);
            matToOutput(outputMat);//RestartCoroutine(matToOutput(outputMat));
        }

        sliderAlpha.value = 0;
        sliderBeta.value = 0;
        scrollBarH.value = 0;
        sliderR.value = 1;
        sliderG.value = 1;
        sliderB.value = 1;    
    }

    public void saveChanges(){
        if(outputMat == null)
            return;

        outputMat.copyTo(img);
        resetUI();
    }

    void detailEnhance(float alpha, float beta){
        if(img == null) return;//yield return null;
        if(beta==0) beta+=0.001f;
        
        // var task = Task.Run(() => { OpenCVForUnity.PhotoModule.Photo.detailEnhance(img, outputMat, alpha, beta);});
        if(newThread!=null)
            newThread.Abort();
        newThread = new Thread(() => {
            try{
                OpenCVForUnity.PhotoModule.Photo.detailEnhance(img, outputMat, alpha, beta);
                shouldUpdatePreview = true;
            } catch (ThreadAbortException ex) {
                Console.WriteLine("Thread is aborted and the code is " + ex.ExceptionState);
            }
        });   
        newThread.Start();  
        // yield return new WaitUntil(() => task.IsCompleted);
    }
    void illuminationChange(float alpha, float beta){
        if(img == null) return; //yield return null;
       
        // var task = Task.Run(() => { OpenCVForUnity.PhotoModule.Photo.illuminationChange(img, mask, outputMat, alpha, beta); });
        if(newThread!=null)
            newThread.Abort();
        newThread = new Thread(() => {
            try{
                OpenCVForUnity.PhotoModule.Photo.illuminationChange(img, mask, outputMat, alpha, beta);
                shouldUpdatePreview = true;
            } catch (ThreadAbortException ex) {
                Console.WriteLine("Thread is aborted and the code is " + ex.ExceptionState);
            }
        });   
        newThread.Start();  
        // yield return new WaitUntil(() => task.IsCompleted);
        // matToOutput(outputMat);//RestartCoroutine(matToOutput(outputMat));
    }
    void deNoise(float h){
        if(img == null) return;//yield return null;
        
        if(newThread!=null)
            newThread.Abort();
        newThread = new Thread(() => {
            try{
                OpenCVForUnity.PhotoModule.Photo.fastNlMeansDenoising(img, outputMat, h);
                shouldUpdatePreview = true;
            } catch (ThreadAbortException ex) {
                Console.WriteLine("Thread is aborted and the code is " + ex.ExceptionState);
            }
        });   
        newThread.Start();
    }
    void colorChangeSlider(float r, float g, float b) {
        if(img == null) return;//yield return null;

        // var task = Task.Run(() => { OpenCVForUnity.PhotoModule.Photo.colorChange(img, mask, outputMat, r, g, b); });
        // yield return new WaitUntil(() => task.IsCompleted);

        if(newThread!=null)
            newThread.Abort();
        newThread = new Thread(() => {
            try{
                OpenCVForUnity.PhotoModule.Photo.colorChange(img, mask, outputMat, r, g, b);
                shouldUpdatePreview = true;
            } catch (ThreadAbortException ex) {
                Console.WriteLine("Thread is aborted and the code is " + ex.ExceptionState);
            }
        });  
        newThread.Start();  
    }
}