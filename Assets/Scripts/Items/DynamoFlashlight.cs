using System.Collections;
using UnityEngine;
using TMPro;
using System;

public class DynamoFlashlight : MonoBehaviour
{
    /**Configuració de la bateria*/
    //Duració en segons de la bateria(2 minuts)
    public float timeToEmpty = 120f;
    //Temps necessari de recarrega del 0 al 100%
    public float timeToRecharge = 5f;
    /**Intensitat maxima de la llanterna*/
    public float maxIntensity = 1000f;

    /**Controls i referencies*/
    public Light spotlight;
    public GameObject efectesVolumetrics;
    public KeyCode toggleKey = KeyCode.F;
    public KeyCode rechargeKey = KeyCode.R;//Mantenint apretat

    public TextMeshProUGUI batteryText;

    [HideInInspector] public bool inputRechargeHeld = false;

    /**Animació llanterna*/
    private Quaternion initialCrankRotation;
    private float currentCrankAngle = 0f;
    private bool isFinishingCrank = false;
    private float crankVelocity = 0f;
    private float targetCrankAngle = 0f;
    public Transform crankTransform;
    public float crankSpeed = 360f;
    public Vector3 rotationAxis = new Vector3(-1,0,0);

    private float currentBatteryPercentage = 100f;
    private bool isOn = false;
    private bool wantsToBeOn = false;
    private bool isRecharging = false;
    private bool isFlickering = false;

    private bool hasFlickered15 = false;
    private bool hasFlickered10 = false;

    public DialegManager dialegManager;
    private int ultimAvisBateria = 100; 

    public float recarregaInicial = 5f;  
    public float recarregaMaxima = 30f;  
    public float tempsFriccio = 2.5f;   
    private float tempsPrementBoto = 0f;
    public float velocitatManivelaInicial = 150f; 
    public float velocitatManivelaMaxima = 600f;
    private float velocitatActualManivela = 0f;   

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentBatteryPercentage = 100f;
        if (spotlight != null)
        {
            spotlight.enabled = false;
            spotlight.intensity = maxIntensity;
        }

        if (efectesVolumetrics != null)
        {
            efectesVolumetrics.SetActive(false);
        }

        if(crankTransform != null)
        {
            initialCrankRotation = crankTransform.localRotation;
        }
    }

    void Update()
    {
        // 1. Utilitzem la variable controlada pel PlayerController en lloc de l'Input
        if (inputRechargeHeld)
        {
            if (!isRecharging && currentBatteryPercentage < 100f)
            {
                isRecharging = true;
                isFinishingCrank = false;
                if (isOn) TurnOffLight();
            }
        }
        else
        {
            if (isRecharging)
            {
                StopRecharging();
                tempsPrementBoto = 0f; 
            }
        }

        // 2. La resta de l'Update és EXACTAMENT el teu original
        if (isRecharging)
        {
            RechargeBattery();
            AnimateCrank();

            if (spotlight != null)
            {
                spotlight.enabled = true;
                spotlight.intensity = maxIntensity * (currentBatteryPercentage / 100f);
            }

            if (efectesVolumetrics != null)
            {
                efectesVolumetrics.SetActive(true);
            }

            if (currentBatteryPercentage >= 100f)
            {
                StopRecharging();
                tempsPrementBoto = 0f;
            }
        }
        else
        {
            if (isFinishingCrank)
            {
                FinishCrankRotation();
            }

            if (wantsToBeOn && currentBatteryPercentage > 0 && !isOn)
            {
                TurnOnLight();
            }
            else if (!wantsToBeOn && spotlight != null && spotlight.enabled && !isFinishingCrank)
            {
                TurnOffLight();
            }

            // ATENCIÓ: Hem esborrat "HandleInput();" d'aquí sota. 
            // Ja no el cridem des de l'Update.

            if (isOn)
            {
                DrainBattery();
                HandleFlickering();

                if (!isFlickering && spotlight != null)
                {
                    spotlight.intensity = maxIntensity * (currentBatteryPercentage / 100f);
                }
            }
        }

        UpdateUIText();
    }

    void UpdateUIText()
    {
        if (batteryText != null)
        {
            int displayPercentage = Mathf.RoundToInt(currentBatteryPercentage);
            batteryText.text = displayPercentage + "%";
        }
    }

    void StopRecharging()
    {
        isRecharging = false;
        if (crankTransform != null)
        {
            isFinishingCrank = true;
            crankVelocity = crankSpeed;
            if(crankSpeed > 0)
            {
                targetCrankAngle = Mathf.Ceil(currentCrankAngle / 360f) * 360;
            } else
            {
                targetCrankAngle = Mathf.Floor(currentCrankAngle / 360f) * 360f;
            }
        }
    }

    public void ToggleLightInput()
    {
        if (!isRecharging)
        {
            wantsToBeOn = !wantsToBeOn; 

            if (wantsToBeOn && currentBatteryPercentage > 0)
            {
                TurnOnLight();
            }
            else
            {
                TurnOffLight();
            }
        }
    }

    void TurnOnLight()
    {
        isOn = true;
        if (spotlight != null)
        {
            spotlight.enabled = true;
        }

        if (efectesVolumetrics != null) efectesVolumetrics.SetActive(true);
    }

    void TurnOffLight()
    {
        isOn = false;
        isFlickering = false;
        if (spotlight != null)
        {
            spotlight.enabled = false;
        }

        if (efectesVolumetrics != null) efectesVolumetrics.SetActive(false);
    }

    void DrainBattery()
    {
        float drainRate = 100f / timeToEmpty;
        currentBatteryPercentage -= drainRate * Time.deltaTime;

        // --- INICI DEL CODI DELS DIÀLEGS ---
        int percentatgeActual = Mathf.RoundToInt(currentBatteryPercentage);

        // Si estem en un múltiple de 10 i hem baixat des de l'últim avís
        if (percentatgeActual % 10 == 0 && percentatgeActual < ultimAvisBateria && percentatgeActual > 0)
        {
            ultimAvisBateria = percentatgeActual; // Registrem l'avís perquè no es repeteixi contínuament

            if (dialegManager != null)
            {
                dialegManager.ReproduirAvisBateria(percentatgeActual);
            }
        }
        // --- FINAL DEL CODI DELS DIÀLEGS ---

        if (currentBatteryPercentage <= 0)
        {
            currentBatteryPercentage = 0;
            TurnOffLight();
        }
    }

    void RechargeBattery()
    {
        tempsPrementBoto += Time.deltaTime;
        float currentRechargeRate = Mathf.Lerp(recarregaInicial, recarregaMaxima, tempsPrementBoto / tempsFriccio);
        currentBatteryPercentage += currentRechargeRate * Time.deltaTime;
        if (currentBatteryPercentage >= 100f)
        {
            currentBatteryPercentage = 100f;
            ultimAvisBateria = 100;
        }
        if (currentBatteryPercentage > 15f)
        {
            hasFlickered15 = false;
        }
        if (currentBatteryPercentage > 10f)
        {
            hasFlickered10 = false;
        }
    }

    void AnimateCrank()
    {
        if (crankTransform != null)
        {
            float percentatge = Mathf.Clamp01(tempsPrementBoto / tempsFriccio);
            velocitatActualManivela = Mathf.Lerp(velocitatManivelaInicial, velocitatManivelaMaxima, percentatge);
            currentCrankAngle -= velocitatActualManivela * Time.deltaTime;
            crankTransform.localRotation = initialCrankRotation * Quaternion.Euler(rotationAxis * currentCrankAngle);
        }
    }

    void FinishCrankRotation()
    {
        if (crankTransform != null)
        {
            currentCrankAngle = Mathf.SmoothDamp(currentCrankAngle, targetCrankAngle, ref crankVelocity, 0.15f, Mathf.Abs(velocitatManivelaMaxima), Time.deltaTime);
            if (Mathf.Abs(targetCrankAngle - currentCrankAngle) < 0.5f)
            {
                currentCrankAngle = targetCrankAngle;
            }
            crankTransform.localRotation = initialCrankRotation * Quaternion.Euler(rotationAxis * currentCrankAngle);
            if (Mathf.Approximately(currentCrankAngle, targetCrankAngle))
            {
                isFinishingCrank = false;
                currentCrankAngle = 0f;
                velocitatActualManivela = 0f;
                crankTransform.localRotation = initialCrankRotation;
            }
        }
    }

    void HandleFlickering()
    {
        if (isFlickering)
        {
            return;
        }
        if(currentBatteryPercentage <= 15 && currentBatteryPercentage > 10f && !hasFlickered15)
        {
            StartCoroutine(FlickerEffect(1));
            hasFlickered15 = true;
        } else if (currentBatteryPercentage <=10f && currentBatteryPercentage > 5f && !hasFlickered10) {
            StartCoroutine(FlickerEffect(3));
            hasFlickered10= true;
        } else if (currentBatteryPercentage <= 5f && currentBatteryPercentage > 0f)
        {
            bool noiseState = Mathf.PerlinNoise(Time.time * 15f, 0f) > 0.5f;
            spotlight.enabled = noiseState;
            if (efectesVolumetrics != null) efectesVolumetrics.SetActive(noiseState);
        }
    }

    IEnumerator FlickerEffect(int flickerCount)
    {
        isFlickering = true;

        for (int i = 0; i < flickerCount; i++)
        {
            spotlight.enabled = false;
            if (efectesVolumetrics != null) efectesVolumetrics.SetActive(false);
            yield return new WaitForSeconds(0.15f);
            spotlight.enabled = true;
            if (efectesVolumetrics != null) efectesVolumetrics.SetActive(true);
            yield return new WaitForSeconds(0.2f);
        }
        isFlickering = false;
    }

    // --- NOUS MÈTODES PER AL CONTROLADOR ---

    public void ToggleLight()
    {
        if (!isRecharging)
        {
            wantsToBeOn = !wantsToBeOn;
        }
    }

    public void StartRecharging()
    {
        if (!isRecharging && currentBatteryPercentage < 100f)
        {
            isRecharging = true;
            isFinishingCrank = false;
            if (isOn) TurnOffLight();
        }
    }

    public void StopRechargingManual() // Ho hem de dir diferent per no interferir amb el privat
    {
        if (isRecharging)
        {
            StopRecharging();
            tempsPrementBoto = 0f;
        }
    }

    public bool IsRecharging()
    {
        return isRecharging;
    }
}
