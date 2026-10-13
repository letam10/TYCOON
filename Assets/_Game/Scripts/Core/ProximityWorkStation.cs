using UnityEngine;

namespace TYCOON
{
    /// <summary>Standing at a workstation performs the same transaction as E.</summary>
    public sealed class ProximityWorkStation : MonoBehaviour
    {
        [SerializeField] private InteractionTarget target;
        [SerializeField] private PlayerInteractor player;
        [SerializeField] private AudioClip workSound;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Min(0.1f)] private float interval = 0.45f;
        private float nextUse;

        public void Configure(InteractionTarget station, PlayerInteractor actor, AudioClip sound = null)
        {
            target = station;
            player = actor;
            workSound = sound;
            audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (player == null || target == null || !target.isActiveAndEnabled || Time.time < nextUse) return;
            // Kho tự nhận hàng; lấy hàng ra kho vẫn dùng E để tránh gửi/lấy lặp liên tục.
            if (target is StorageInteractionTarget && player.Carry.Inventory.TotalCount == 0) return;
            player.RefreshTarget();
            if (player.CurrentTarget != target) return;
            nextUse = Time.time + interval;
            // Tự thao tác dùng chung giao dịch đã kiểm tra; không sửa số lượng hoặc ví trực tiếp.
            bool interacted = player.TryInteract();
            bool operated = target is MachineInteractionTarget machine && machine.TryOperate(player, interval);
            if ((interacted || operated) && audioSource != null && workSound != null)
                audioSource.PlayOneShot(workSound, 0.3f);
        }
    }
}
