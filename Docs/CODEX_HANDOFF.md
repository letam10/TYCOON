# TYCOON — CODEX HANDOFF

## Công đoạn vừa hoàn thành

**PurchaseProgress persistence và phân biệt mua ngay với góp tiền từng phần.** Runtime giữ riêng khoản góp dở của ô mua trong world save; giao dịch mua ngay vẫn yêu cầu đủ phần còn lại, còn `TryContributePurchase` cho phép góp theo số tiền hiện có. Save prototype v1 không bị ghi đè và save cũ thiếu trường mới được hiểu là không có khoản góp dở.

## Hệ thống và file chính đã thay đổi

- `Assets/_Game/Scripts/Core/GameSession.cs`: tách validation mua đủ tiền khỏi góp từng phần; `TryPurchase`/`CanPurchase` giữ strict semantics, `TryContributePurchase` chỉ yêu cầu còn tiền và vẫn debit tối đa phần còn thiếu.
- `Assets/_Game/Scripts/Save/GameSaveData.cs`: thêm `PurchaseProgressState[] purchaseProgress` vào world snapshot.
- `Assets/_Game/Scripts/Save/JsonSaveStore.cs`: validate ID, tổng giá, số đã góp và trạng thái pending; JSON cũ thiếu trường vẫn tương thích như danh sách rỗng.
- `Assets/_Game/Scripts/Save/SaveCoordinator.cs`: capture và restore purchase progress qua `ProgressionState`, dùng chung cho v1 world save và v2 world payload.
- `Assets/_Game/Scripts/Tests/Save/PlayMode/SaveCoordinatorTests.cs`: kiểm tra góp 30/80, thay đổi runtime, load lại giữ 30/80 và ví đúng.
- Giữ nguyên order transaction, stable ID, inventory ownership, Input System, pooling, machine operator token, CLI/editor bridge và Save v2 customer runtime từ các commit trước.

## Trạng thái hiện tại

- Unity `6000.6.3f1`, URP `17.6.0`, Windows offline single-player.
- Editor reload mới: `scriptCompilationFailed=false`, compiler issue `0`, console sạch trước khi test.
- EditMode mới: `59/59` pass tại `TestResults/codex-purchase-progress-editmode-20261013-150000.xml`.
- PlayMode bridge đã có một lượt `58/59` pass; failure duy nhất là test chuỗi processor/restocker cũ, không phải test purchase/save. Một lượt sau Unity Test Framework dừng tại `PlayModeRunTask` với `NullReferenceException` trước khi có test count. Batch Unity compile/import log kết thúc bằng return code `0` nhưng `-runTests` không tạo XML, nên chưa coi PlayMode toàn bộ là pass.
- Không có Windows player build hoặc visual acceptance trong milestone này.
- Save v2 vẫn chưa lưu/khôi phục đầy đủ crew/worker task state, conveyor in transit, animal cycles, event/repair state, restaurant table state hoặc simulation clock.
- Farm Shop, Processing, Supermarket, Bakery và Restaurant chưa đạt nội dung/scene integration đầy đủ của objective lớn.
- Dirty/untracked files có trước trong worktree được giữ nguyên và không stage.

## Commit và push GitHub

Đang hoàn tất kiểm tra staged scope; commit hash và remote hash sẽ được ghi bổ sung ngay sau khi commit/push của milestone này. Không có file ngoài danh sách milestone được stage.
