# TYCOON — CODEX HANDOFF

## Công đoạn vừa hoàn thành

**Audit baseline và order transaction foundation.** Baseline ownership của item, inventory/store, checkout/payment/cash, machine, purchase/unlock, save/load, worker và interaction vẫn giữ nguyên. Customer order hiện có state giao dịch riêng để tránh thanh toán hoặc thất thoát lặp.

## Hệ thống và file chính đã thay đổi

- `OrderState.cs` thêm `OrderStatus`, line yêu cầu/đã giao, giá chốt, patience và snapshot/restore có validation.
- `CustomerAgent.cs` tạo order tại thời điểm chọn hàng, ghi nhận giao hàng khi lấy từ shelf, giảm patience trong hàng chờ và chuyển order sang thất bại idempotent khi rời hàng.
- `CheckoutStation.cs` chỉ ghi sale khi order đã giao đủ; payment vẫn qua `CheckoutLedger` và không tự tăng ví.
- `CustomerFlowTests.cs` thêm case timeout 90 giây: khách giữ phần hàng đã nhận, không thanh toán, ledger ghi loss một lần.
- `OrderStateTests.cs` kiểm tra giao từng phần, thanh toán một lần, failure terminal và snapshot round trip.
- Giữ nguyên stable ID, Input System, pooling, inventory primitives, route ownership, machine operator token, purchase progress và CLI/editor bridge.

## Trạng thái hiện tại

- Unity `6000.6.3f1`, URP `17.6.0`, Windows offline single-player.
- Compile batch mới: exit `0`, không có `error CS`, `Scripts have compiler errors` hoặc `Compilation failed`.
- EditMode mới: `56/56` pass.
- PlayMode mới: `57/57` pass.
- Khách hết patience được ghi thất thoát phần hàng đã nhận; khách nhận đủ chỉ thanh toán một lần.
- Save v1 vẫn giữ barrier khi customer còn giữ hàng; Save v2 đầy đủ order queue, worker/crew, conveyor, animal và event state vẫn là phạm vi tiếp theo.
- Chưa triển khai nội dung Farm Shop, Processing, Supermarket, Bakery hoặc Restaurant; scene integration và Windows build chưa được coi là hoàn tất.
- Các file dirty/untracked có trước trong worktree được giữ nguyên và không đưa vào commit của milestone này.

## Commit và push GitHub

Đã commit milestone order transaction và push lên `origin/main`. Sau push, `git rev-parse HEAD` và `git ls-remote origin refs/heads/main` phải trùng; các thay đổi ngoài danh sách milestone không được stage.
