# TYCOON — CODEX HANDOFF

## Công đoạn vừa hoàn thành

**Công đoạn 01 — Audit và baseline.** Đã rà soát ownership của item, inventory/store, customer/order, checkout/payment/cash, machine, purchase/unlock, save/load, NPC navigation, upgrade, worker và interaction. Các luồng prototype mâu thuẫn đã được chặn hoặc thay bằng transaction có owner rõ ràng.

## Hệ thống và file chính đã thay đổi

- `ItemInventory` giữ primitive inventory hiện có và có thao tác `Clear` nguyên tử.
- `CheckoutLedger` ghi thất thoát theo stable item ID, giữ pending cash và receipt/sale idempotency.
- `CustomerAgent`, `Shelf`, `CheckoutStation` không hoàn hàng khách đã cầm khi hủy hoặc hết kiên nhẫn; hàng bị mất được ghi vào ledger.
- `SaveCoordinator` không còn chiếu cart khách ngược về shelf. Save v1 từ chối capture/load khi khách còn giữ hàng để tránh mất ownership hoặc hoàn hàng ngầm.
- `ProductionMachine` không còn tự `Tick` khi chỉ bật component. Máy chỉ chạy khi có operator token; player và `ProcessorJob` dùng cùng cơ chế reservation/operation.
- `MachineInteractionTarget` và `ProximityWorkStation` vận hành máy trong vùng thao tác; rời vùng giữ tiến độ và dừng máy.
- `InventoryStore` có `OwnerScopeId` suy ra từ stable ID/parent và whitelist route nguồn → đích. `RestockerJob` lọc nguồn theo route, không coi toàn bộ output và shelf là một kho global.
- `GameSession` có `PurchaseProgressState`; `PurchasePad` góp tiền theo thời gian khi player đứng trong trigger, dừng khi rời vùng, lưu phần đã góp và chỉ unlock một lần khi đủ giá.
- Giữ nguyên stable ID, Input System, pooling, inventory primitives và CLI/editor bridge.
- Cập nhật focused tests trong `Assets/_Game/Scripts/Tests/Commerce` cho abandonment/loss và save barrier.

## Trạng thái hiện tại

- Unity `6000.6.3f1`, URP `17.6.0`, Windows offline single-player.
- Compile batch mới nhất: exit `0`, không có `error CS`, `Scripts have compiler errors` hoặc `Compilation failed`.
- EditMode mới nhất: `53/53` pass.
- PlayMode scene mới nhất: `56/56` pass.
- Chưa triển khai thêm nội dung Farm Shop, Processing, Supermarket, Bakery hoặc Restaurant trong công đoạn này; chỉ sửa các luồng prototype để baseline không mâu thuẫn với gameplay mới.
- Save v1 hiện dùng barrier khi customer còn unsettled; OrderState/CrewState đầy đủ là phạm vi tiếp theo, không được giả lập bằng hoàn hàng.

## Commit và push GitHub

Đã commit với thông điệp `baseline: enforce ownership and operator transactions` và push lên `origin/main`. Sau push, `git ls-remote origin refs/heads/main` đã được đối chiếu và trùng `HEAD`.
