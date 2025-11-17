using UnityEngine;

public class RecoilSystem : MonoBehaviour
{
    [Header("Recoil Settings")]
    public float recoilUp = 6f;
    public float recoilSide = 2f;
    public float snappiness = 10f;
    public float returnSpeed = 4f;

    private Vector2 currentRecoil;
    private Vector2 targetRecoil;

    void Update()
    {
        targetRecoil = Vector2.Lerp(targetRecoil, Vector2.zero, returnSpeed * Time.deltaTime);
        currentRecoil = Vector2.Lerp(currentRecoil, targetRecoil, snappiness * Time.deltaTime);

        transform.localRotation = Quaternion.Euler(-currentRecoil.x, currentRecoil.y, 0);
    }

    public void FireRecoil()
    {
        float side = Random.Range(-recoilSide, recoilSide);
        targetRecoil += new Vector2(recoilUp, side);
    }
}
