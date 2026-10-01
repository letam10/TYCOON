# TYCOON — Kiến trúc đề xuất

Ngày lập: 2026-10-01 (Asia/Bangkok). Trạng thái: **PROPOSED — chưa xác minh triển khai**.

Nguồn yêu cầu: master task TYCOON, mục 0–58. Tài liệu này mô tả thiết kế cần thực hiện; tên component, asset và test dưới đây là đề xuất, không phải bằng chứng chúng đã tồn tại. Khi bắt đầu phần tài liệu, thư mục gốc chỉ có `ASSET/`, `README.md` và Git; chưa quan sát được Unity project. `PLAN.md` là nguồn sự thật về tiến độ; bằng chứng nghiệm thu nằm trong `Documentation/QA.md`.

## 1. Ranh giới sản phẩm và thứ tự triển khai

- Project chính: `D:\APP\TYCOON`; Unity 6, URP, C#, Windows x64. Phiên bản Unity/package chỉ được ghi là xác nhận sau khi kiểm tra editor thực tế, `ProjectSettings/ProjectVersion.txt` và `Packages/manifest.json`.
- Một game liên tục: Farm → Farm Shop → Processing → Supermarket → Bakery/Restaurant. Mỗi khu sau tiêu thụ hàng hoặc sử dụng hệ thống khu trước; không tạo năm game tách biệt.
- Gate đầu: player/camera ổn định trước Farm. Slice đầu phải chơi từ manual work → first automation → shop → processing, gồm 1 player; Carrot/Tomato/Wheat; Cow/Chicken; Milk/Egg; 1 máy; 2 kệ; 1 checkout; 10–15 khách; Farmer/Restocker/Cashier; purchase pads; money; upgrades; save/load.
- Supermarket và bakery progression vẫn là gate cuối theo master task §53. Slice đầu đạt không có nghĩa toàn bộ nhiệm vụ đạt.
- Mỗi milestone qua compile/static, Unity automated tests và quan sát game thực tế trước khi làm milestone tiếp theo. Không chuyển lỗi nghiêm trọng sang vòng sau.

## 2. Cấu trúc dự kiến

```text
ASSET/                              # USER_PROVIDED, nguồn bất biến
ArtSource/Processed/                # Sản phẩm xử lý, không phải bản backup
Tools/Blender/                      # Script inspect/process/export có thể chạy lại
Assets/_Game/
  Art/Imported/ Audio/ Data/ Materials/ Prefabs/
  Scenes/ Shaders/ UI/ VFX/
  Scripts/
    Core/ Player/ Economy/ Items/ Production/ Farming/ Animals/
    Customers/ Workers/ Shops/ Upgrades/ Save/ UI/ Debug/ Editor/ Tests/
Documentation/                      # Audit, architecture, QA, blockers, report
Screenshots/                        # Ảnh thật từ game/editor/build đang chạy
work/                               # Tạm thời của tác vụ, dọn theo quyền sở hữu
Build/Windows/TYCOON.exe             # Bản giao Windows; không commit Build/
```

Chỉ thêm lớp hoặc interface khi có nhiều consumer thực tế hoặc ranh giới test hữu ích. Không dùng một MonoBehaviour quản lý toàn game. Debug helpers chỉ phục vụ quan sát/tạo fixture; luồng nghiệm thu từ save sạch phải không phụ thuộc cheat.

## 3. Runtime: trách nhiệm nhỏ và giao dịch rõ ràng

| Nhóm | Component/config đề xuất | Trách nhiệm và invariant |
| --- | --- | --- |
| Core | `GameBootstrap`, `ProgressionController` | Ghép reference, load save, khởi tạo khu đã unlock; không chứa logic bán hàng hay di chuyển. |
| Player | `PlayerInputReader`, `PlayerMotor`, `PlayerInteraction`, `CarryInventory` | Input actions tách khỏi locomotion; WASD và gamepad; movement bằng code, animation khớp vận tốc; tương tác theo zone. |
| Camera | Follow target + Cinemachine tương thích | Góc pitch 45–60°, damping ổn định, framing nhìn rõ lối đi/stack/pad; không cần free camera ở slice đầu. |
| Items | `ItemDefinition`, `ItemInventory`, `ItemTransfer`, `InventoryVisual` | Chuyển hàng có kiểm tra số lượng/capacity trước mutation; không âm, không nhân đôi, không mất hàng; phát event sau thành công. |
| Farming/Animals | `CropPlot`, `AnimalProducer` | Thời gian lớn/chu kỳ sản xuất và đầu ra lấy từ config; harvest/collect dùng cùng giao dịch item. |
| Production | `ProductionMachine`, `RecipeDefinition` | Check input + output capacity; nhận đủ nguyên liệu đúng một lần, giữ tiến độ, tạo output đúng một lần; upgrade từ data; visual moving parts phản ánh state. |
| Shops | `Shelf`, `Storage`, `Checkout`, `SaleTransaction` | Kệ trừ hàng khi khách lấy, giỏ khách giữ hàng, checkout hoàn tất sale và thanh toán đúng một lần. Hàng chưa bán phải được theo dõi hoặc trả về khi hủy. |
| Economy | `Wallet`, `PriceDefinition` hoặc giá trong item | Money kiểu số nguyên theo đơn vị game; sale credit và purchase debit có idempotency trong transaction; không có doanh thu timer ẩn. |
| Customers | `CustomerAgent`, queue reservations, customer pool | NavMesh + state machine; demand lựa chọn từ hàng/khu khả dụng; reservation và queue slot được giải phóng khi thoát/hủy. |
| Workers | `WorkerAgent` + job components | Shared locomotion, inventory, state, reservation; mỗi job chỉ quyết định nguồn, đích và thao tác; không viết một AI khác hoàn toàn cho từng nghề. |
| Upgrades | `PurchasePad`, `UpgradeDefinition`, unlock registry | Kiểm tiền và trạng thái trước debit; unlock một lần; animation/VFX/audio/UI là phản hồi của giao dịch đã thành công. |
| Save | `SaveService`, serializable DTOs, stable-ID registry | Lưu dữ liệu thuần theo ID; không serialize scene object reference. Load phục hồi trạng thái trước khi chạy lại simulation. |
| UI/Audio | HUD presenters, contextual audio/VFX hooks | Đọc event và state; không tự thêm tiền, sinh item hoặc đổi progression. UI chỉ hiện trạng thái nghiệp vụ thật. |

Các hệ thống dùng reference rõ trong scene/prefab được editor tool gán tự động. Event cục bộ cho thay đổi inventory, money, sale, unlock và stage; không cần global event bus hoặc service locator nếu chưa có nhu cầu.

## 4. Config và stable IDs

`ItemDefinition` là ScriptableObject có `Id`, `DisplayName`, `Icon`, `Prefab`, `BuyPrice`, `SellPrice`, `StackSize`, `ProductionCategory`. ID không phụ thuộc tên hiển thị, đường dẫn, thứ tự hierarchy hoặc `GetInstanceID`. Validator phải phát hiện ID trùng/rỗng và reference thiếu. Không dispatch logic bằng chuỗi `if itemName == ...`.

Catalog bắt buộc gồm Carrot, Tomato, Wheat, Milk, Egg, Flour, Cheese, Bread và Cake; Sauce và Meal được định nghĩa khi thực hiện chain tương ứng. Recipe chứa ID item đầu vào/đầu ra, quantity, thời gian, capacity và level modifiers. Ví dụ: Wheat→Flour; Milk→Cheese; Tomato→Sauce; Flour+Egg+Milk→Bread/Cake. Công thức cụ thể và economy tuning phải nằm trong data, được ghi lại khi nghiệm thu.

Upgrade config chứa stable ID, cost, prerequisites, unlock target, level và giới hạn. Stage config chỉ unlock các khu trong cùng map; manual actions và worker actions gọi cùng operations để automation thực sự thay thế việc người chơi đã làm.

Inventory có quantity chính xác độc lập với model hiển thị. `InventoryVisual` dùng slot/pool hoặc representative stack; 0 phải rỗng, 5 và 20 phải phân biệt được nếu capacity cho phép. Áp dụng cho player carry, storage, shelf và machine output. Không cần một GameObject cho mỗi đơn vị hàng.

## 5. Customer và worker lifecycle

Customer: `Spawn → Enter → SelectProduct → GoToShelf → TakeProduct → FindCheckout → Queue → Pay → Exit → ReturnToPool`. Tất cả di chuyển sử dụng NavMesh hợp lệ. Khi hàng hết, tuyến bị chặn hoặc checkout chưa mở, có đường chuyển trạng thái rõ: chọn lại, chờ có giới hạn hoặc rời đi; không đứng vô hạn giữ reservation. Chỉ `Pay` với giỏ hợp lệ và checkout hoàn tất mới sinh sale credit.

Pool reset phải xóa giỏ/reservation/path/animation/payment flag trước lần dùng mới. Đích đầu là 10–15 khách trong slice; gate stress mở lên 30 khách trước tối ưu thêm. Số khách hoặc thời gian không được tự sinh doanh thu.

Worker dùng state chung: `Idle → SelectJob → ReserveSource/Target → Move → Work/Pickup → Carry/Deliver → Release → Idle`, bỏ qua bước không cần cho nghề cụ thể. Jobs: Farmer, AnimalWorker, Restocker, Cashier, Processor, Cook, Waiter. Slice nghiệm thu Farmer/Restocker/Cashier; các job khác phục vụ các khu kế tiếp. Người và worker tranh nguồn/đích phải qua reservation và kiểm tra capacity; cancellation giải phóng tài nguyên.

## 6. Save/load và tiến trình

Save tối thiểu money, unlock IDs, upgrade levels, business stage, employee unlocks, production progress quan trọng. Lưu inventory bền vững và input đã được nhận vào máy để restart không tạo/mất hàng. Mỗi entity cần persistence có stable ID; DTO có schema version rõ. Save ghi hoàn chỉnh qua file tạm do tác vụ sở hữu rồi thay file đích; không tự tạo `.bak`, archive hoặc snapshot.

Load dựng khu/upgrade/worker đã mở trước, rồi inventory và production; clamp/check theo schema đã biết, báo lỗi rõ khi dữ liệu không hợp lệ. Customer transient được khởi tạo lại theo quy tắc đã ghi và kiểm thử; không trả lại sale đã thanh toán hoặc làm biến mất giá trị hàng đang checkout. Quy tắc save cho transaction đang chạy phải được quyết định và test trước milestone save/load, không che bằng việc chỉ test lúc idle.

Save QA dùng profile/thư mục test riêng để bắt đầu sạch, không xóa save người dùng. Bản build và editor cùng kiểm tra quit/restart/load; hot reload trong Play Mode không đủ.

## 7. Pipeline nguồn, split, rig và animation

`ASSET/ → Blender processing → ArtSource/Processed/ → Assets/_Game/Art/Imported/`. Trước xử lý, ghi hash, signature và parser thực tế trong `ASSET_AUDIT.md`. Đặc biệt phân biệt `Item3.blend`, `.fbx`, `.glb` bằng header/parse; không đổi tên extension hoặc import chỉ vì tên trông đúng. Nguồn giữ nguyên; derivative có source mapping. Model nguồn: `USER_PROVIDED`, `LICENSE_METADATA_UNKNOWN` khi thiếu metadata; được dùng nội bộ, không tự public/relicense.

Blender inspect object hierarchy, connected components, material/UV islands, connectivity và hình dạng để split bộ phận độc lập. Không dùng vertex-count threshold làm bằng chứng semantic split. Trường hợp không chắc phải mở Blender và quan sát. Sau split kiểm normals, pivot/origin, transforms, scale, materials, UV; screenshot pose/Inspector nối với asset ID.

Character dùng humanoid rig và pose tests hips/spine/chest/neck/head/arms/hands/legs/feet. Cow dùng quadruped, Chicken dùng bird rig; kiểm skin weights bằng idle/walk/run test, không chỉ auto-weight. Prop/machine chỉ rig nếu cần deformation; moving part dùng child transforms/Animator. Không rig toàn máy chỉ để quay wheel hoặc conveyor.

Clip cần có: Player Idle/Walk/Run/CarryIdle/CarryWalk/Pickup/Drop; Worker Idle/Walk/Work/Carry; Customer Idle/Walk/Carry hoặc Shopping/Checkout; Cow Idle/Walk/Eat; Chicken Idle/Walk/Peck. Locomotion dùng code, tốc độ clip khớp tốc độ di chuyển để giảm foot sliding. Nguồn animation phải original, user-provided hoặc có license hợp lệ.

## 8. Presentation và performance

URP stylized sáng, mềm, dễ đọc; palette thống nhất, bóng mềm, ambient, SSAO/reflection/post processing vừa đủ khi tương thích. Material phân biệt Wood/Metal/Painted Metal/Plastic/Soil/Grass/Stone/Fabric/Food; zero pink hoặc missing texture. Visible important objects dùng model thật sau art pass; primitive chỉ prototype/collider/debug.

HUD gồm money, carried item, capacity, objective, upgrade price, shop status; final UI sạch, bo góc, không giữ default Button làm giao diện cuối. Audio có pickup/drop/money/purchase/machine/customer/farm/animals/UI. Coin/item pop, purchase reveal, reaction, particles, machine motion và UI tween phải hỗ trợ phản hồi, không lấn gameplay.

Thiết kế cho 60 FPS, 1080p trên gaming PC tầm trung: pooling customer/item visuals/VFX; instancing/batching vật liệu phù hợp; LOD/culling/shadow budgets theo đo lường. Không thêm nhiều Animator skeleton không cần thiết. GPU tác vụ vừa/nặng chỉ được khởi chạy sau khi backend xác nhận RTX 4060; không fallback iGPU. Kết quả benchmark phải ghi CPU/GPU/driver, resolution, quality, build, workload và frame-time.

## 9. Editor tooling và kiểm chứng

Tool trong `Scripts/Editor/`: bootstrap scene, generate/configure prefab, material conversion, import validator, stable-ID/config validator, NavMesh setup/bake, build Windows. Tool phải xác định object/asset được quản lý, chạy lại không tạo duplicate hoặc ghi đè chỉnh sửa ngoài phạm vi. Package URP/Input System/Cinemachine/AI Navigation/Test Framework chỉ pin sau khi kiểm tra compatibility của Unity thực tế; không đoán API/version.

EditMode test logic thuần: inventory transfers, recipe consumption/output, wallet/sales, prerequisites, stable-ID validation và save round trip. PlayMode test integration: player interactions, customer/worker lifecycle, queue, production, purchase replay, save mid-operation. Visual QA quan sát Play Mode và build thật; screenshot không thay thế test giao dịch. `Documentation/QA.md` định nghĩa từng gate và evidence.

Giữ Unity Editor mở trong phiên khi có thể. Computer Use phải kiểm tra tool/runtime, import thực tế, exposed API và targets trước thao tác; không coi lời xác nhận cũ là bằng chứng. Nếu `node_repl`/`@oai/sky` không dùng được, ghi diagnostics/blocker và dùng automated PlayMode, editor screenshots, build/run/log evidence theo khả năng; không báo manual QA đã chạy.

## 10. Delivery và vận hành

Không commit `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`; LFS nếu có cho binary mới lớn, không rewrite history. Chỉ commit milestone ổn định thuộc tác vụ sau diff/test, push ngay, ghi rõ khi push lỗi. Không commit nguồn có license unknown lên chỗ public mới hoặc tự relicense.

Sau mỗi major task cập nhật PLAN/DEVLOG/QA/BLOCKERS bằng kết quả thật. Hai lần sửa cùng lỗi chưa thành công phải ghi exact error, minimal repro, root cause, version compatibility, nguồn chính thức và chosen fix trước khi sửa tiếp. Ít nhất ba polish loops riêng: gameplay, visual, QA/performance.

Trước bàn giao kiểm toàn bộ process tree do task tạo bằng PID, parent PID, command line; dừng process không còn cần, xác nhận đã thoát, rồi dọn đúng temporary files. Giữ editor/build session còn phục vụ kết quả chỉ khi ghi PID/mục đích/điều kiện dừng. `FINAL_REPORT.md` phải phản ánh build/test/performance/known issues thật; sự tồn tại của executable hoặc compile pass chưa chứng minh game hoàn thành.

## 2026-10-01 implementation evidence

Framework components now compile and pass 53 EditMode plus 33 PlayMode tests. The save backend, inventories, commerce ledger, NavMesh actor components, production, player, progression and HUD data are implemented. SaveCoordinator, authored world/data/prefab assembly, full progression loop, model/animation processing and release validation remain integration work. Earlier proposed architecture remains the target, not proof of completed scene behavior.
