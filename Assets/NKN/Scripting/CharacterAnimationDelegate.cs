using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAnimationDelegate : MonoBehaviour
{
    [Header("Puntos de ataque cuerpo")]
    public GameObject LFootAttackPoint, RFootAttackPoint;
    public GameObject LHandAttackPoint, RHandAttackPoint;

    [Header("Armas")]
    public GameObject KatanaAttackPoint;

    [Header("Kunai")]
    public GameObject PKunaiAttackPoint, EKunaiAttackPoint;

    // PIES
    void LFootAttackPoint_On() { LFootAttackPoint.SetActive(true); }
    void LFootAttackPoint_Off() { if (LFootAttackPoint.activeInHierarchy) LFootAttackPoint.SetActive(false); }

    void RFootAttackPoint_On() { RFootAttackPoint.SetActive(true); }
    void RFootAttackPoint_Off() { if (RFootAttackPoint.activeInHierarchy) RFootAttackPoint.SetActive(false); }

    // MANOS
    void LHandAttackPoint_On() { LHandAttackPoint.SetActive(true); }
    void LHandAttackPoint_Off() { if (LHandAttackPoint.activeInHierarchy) LHandAttackPoint.SetActive(false); }

    void RHandAttackPoint_On() { RHandAttackPoint.SetActive(true); }
    void RHandAttackPoint_Off() { if (RHandAttackPoint.activeInHierarchy) RHandAttackPoint.SetActive(false); }

    // KATANA
    void KatanaAttackPoint_On() { KatanaAttackPoint.SetActive(true); }
    void KatanaAttackPoint_Off() { if (KatanaAttackPoint.activeInHierarchy) KatanaAttackPoint.SetActive(false); }

    // KUNAI ENEMIGO
    void EKunaiAttackPoint_On() { EKunaiAttackPoint.SetActive(true); }
    void EKunaiAttackPoint_Off() { if (EKunaiAttackPoint.activeInHierarchy) EKunaiAttackPoint.SetActive(false); }

    // Nota: PKunaiAttackPoint se activa desde PlayerPlay.SpawnKunai()
}
