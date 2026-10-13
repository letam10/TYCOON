# TYCOON — CODEX HANDOFF

## Công đoạn vừa hoàn thành

**Audit baseline, order transaction foundation và Save v2 customer runtime.** Baseline ownership của item, inventory/store, checkout/payment/cash, machine, purchase/unlock, save/load, worker và interaction vẫn giữ nguyên. Customer order có state giao dịch riêng; Save v2 có schema, atomic store, capture và load runtime riêng để bảo toàn cart/order ownership mà không ghi đè save prototype v1.

## Hệ thống và file chính đã thay đổi

- `OrderState.cs` thêm `OrderStatus`, line yêu cầu/đã giao, giá chốt, patience và snapshot/restore có validation.
- `CustomerAgent.cs` có stable customer ID, tạo order tại thời điểm chọn hàng, ghi nhận giao hàng khi lấy từ shelf, giảm patience trong hàng chờ, capture/restore snapshot và chuyển order sang thất bại idempotent khi rời hàng.
- `CustomerPool.cs` gán slot ID deterministic, restore customer theo ID/checkout route và resume đúng pool sau load.
- `Shelf.cs` khôi phục reservation của customer mà không nhập lại hàng; `CheckoutStation.cs` khôi phục FIFO queue index và chỉ ghi sale khi order đã giao đủ.
- `CustomerFlowTests.cs` kiểm tra timeout loss và Save v2 round trip trên customer pool: cart, order, queue, shelf reservation, revenue và loss.
- `GameSaveDataV2.cs` định nghĩa save version 2 với world snapshot, customer order snapshot, cart ownership, queue index, state, visit và source shelf.
- `JsonSaveV2Store.cs` ghi/đọc JSON atomic ở path riêng, không chạm file v1; validator reject duplicate identity, sai cart owner, order line hoặc vị trí không hữu hạn.
- `SaveCoordinator.cs` giữ nguyên v1 barrier; bổ sung `TryCaptureV2`, `TrySaveV2(path)` và `TryLoadV2(path)` dùng world restore chung, reset pool không ghi loss rồi restore order theo FIFO.
- `OrderStateTests.cs` và `SaveV2Tests.cs` kiểm tra transaction, snapshot round trip, duplicate identity và file v1 không bị thay đổi.
- Giữ nguyên stable ID, Input System, pooling, inventory primitives, route ownership, machine operator token, purchase progress và CLI/editor bridge.

## Trạng thái hiện tại

- Unity `6000.6.3f1`, URP `17.6.0`, Windows offline single-player.
- Compile batch mới: exit `0`, không có `error CS`, `Scripts have compiler errors` hoặc `Compilation failed`.
- EditMode mới: `59/59` pass.
- PlayMode mới: `58/58` pass.
- Khách hết patience được ghi thất thoát phần hàng đã nhận; khách nhận đủ chỉ thanh toán một lần.
- Save v1 vẫn giữ barrier khi customer còn giữ hàng. Save v2 đã capture/load được customer order, cart, source shelf reservation, transform/state và FIFO queue trên scene test.
- Save v2 chưa lưu/khôi phục đầy đủ worker/crew state, conveyor in transit, animal cycles, event/repair state, restaurant table state hoặc toàn bộ simulation clock.
- Chưa triển khai đầy đủ nội dung Farm Shop, Processing, Supermarket, Bakery hoặc Restaurant; scene integration toàn game, visual acceptance và Windows build chưa được coi là hoàn tất.
- Các file dirty/untracked có trước trong worktree được giữ nguyên và không đưa vào commit của milestone này.

## Commit và push GitHub

Đã commit milestone Save v2 customer runtime và push lên `origin/main`. Sau push, `git rev-parse HEAD` và `git ls-remote origin refs/heads/main` đã được đối chiếu trùng; các thay đổi ngoài danh sách milestone không được stage.
