# TYCOON — Acceptance và bằng chứng QA

Ngày lập: 2026-10-01 (Asia/Bangkok). **Game tests: NOT RUN. Windows build: NOT RUN.**

Nguồn: master task TYCOON §0–58. Đây là kế hoạch nghiệm thu và nơi ghi kết quả thực tế, không phải báo cáo PASS. Snapshot đầu phần tài liệu chỉ có `ASSET/`, `README.md` và Git; chưa có bằng chứng Unity implementation/runtime. Kiến trúc trong `ARCHITECTURE.md` hiện là đề xuất. `PLAN.md` theo dõi tiến độ authoritative.

## Quy tắc đọc và lưu evidence

- `NOT RUN`: chưa thực thi; `FAIL`: đã chạy và vi phạm; `BLOCKED`: không thể chạy do blocker được ghi; `PASS`: evidence trực tiếp đạt toàn bộ tiêu chí trong row. Không suy ra PASS từ code tồn tại, screenshot tĩnh, compile hoặc ghi chú cũ.
- Mọi evidence path dưới đây là **đích dự kiến**, chưa xác nhận file tồn tại. Chỉ điền liên kết kết quả sau khi kiểm tra nội dung, timestamp, commit/dirty state và workload đúng gate.
- Log mỗi run: run ID, giờ địa phương, commit + thay đổi chưa commit, Unity/URP/package version, scene/build, save profile, CPU/GPU/driver/resolution/quality, test steps/expected/actual, pass/fail và lỗi còn mở. Evidence phải gắn với state đang giao, không lấy kết quả trước sửa làm bằng chứng sau sửa.
- Kết quả Unity XML và tóm tắt: `Documentation/Evidence/<run-id>/`; ảnh game thật: `Screenshots/`; raw log/profiler captures tạm: `work/qa/<run-id>/`. Giữ bằng chứng bàn giao cần thiết đã chọn; dọn file tạm không cần sau khi process kết thúc. Không commit `Logs/` hoặc `Build/`.
- Mỗi major task qua **A compile/static → B automated EditMode/PlayMode → C visual/human-like QA**, fix/retest trước milestone sau. Khi layer C bị giới hạn, ghi rõ layer nào chưa làm; automated test không được đổi nhãn thành manual test.
- Các mốc số lần/thời gian/p95 bên dưới là **tiêu chí QA đề xuất để đo**, bổ sung mục tiêu bắt buộc trong master task, chưa phải kết quả đã đạt.

## Gate 0 — Workspace, capability, asset integrity

| ID / spec | Cần chứng minh | Tiêu chí đạt và evidence cần có | Trạng thái |
| --- | --- | --- | --- |
| G01 / §0,40,58 | Đúng project/Git | Root đúng `D:\APP\TYCOON`, remote `https://github.com/letam10/TYCOON.git`; ghi branch/status, fetch và an toàn merge; diff không chứa thay đổi người dùng ngoài scope, secret, cache/build. Git output + PLAN/DEVLOG. | NOT RUN |
| G02 / §6–8,50,58 | Unity 6 URP và packages phù hợp | Editor thực tế, ProjectVersion và manifest thống nhất; URP active; Input System/Cinemachine/AI Navigation/Test Framework tương thích; zero compiler errors. Version evidence + editor/compilation log. | NOT RUN |
| G03 / §1,16,17 | Nguồn và chữ ký file | Audit mọi source input, ghi hash/header/parser/import outcome; Item3.blend/fbx/glb kiểm riêng và đối chiếu nội dung; file sai/invalid không nhập Unity; hash nguồn trước/sau không đổi. `ASSET_AUDIT.md` + parser outputs. | NOT RUN |
| G04 / §18–21 | Split, rig, pose và clips | Inspect hierarchy/components/material/UV/semantic shape, kiểm normals/pivots/scale/UV; model mơ hồ phải quan sát Blender. Character humanoid, Cow quadruped, Chicken bird; 100% actor chính có pose + animation test, không stretch/collapse rõ; máy không rig khi không cần deformation. Audit + Blender/Unity screenshots. | NOT RUN |
| G05 / §22,23,47 | Provenance/license | Source input ghi USER_PROVIDED và LICENSE_METADATA_UNKNOWN nếu thiếu; online model/texture/audio/animation có nguồn/creator/terms/local destination; imagegen chỉ dùng sau detect và texture phải game-ready; không public/relicense tùy tiện. `ASSET_SOURCES.md`. | NOT RUN |
| G06 / §27–30,51 | Computer Use/editor workflow | Xác minh runtime thực tế, import/API và targets rồi thao tác Unity/Blender; Play/Stop và quan sát Game/Console có evidence. Nếu thiếu capability: exact diagnostics, fallback scope và phần chưa test trong BLOCKERS. Giữ editor session khi phù hợp. | NOT RUN |
| G07 / AGENTS | Thiết bị GPU | Trước job GPU/AI vừa hoặc nặng, backend nhận và chọn RTX 4060; ghi device/PID/command, không fallback iGPU; polling 3/10/30 phút theo tải trừ event kết thúc/lỗi. Environment evidence. | NOT RUN |

## Gate 1 — Player, manual farm và hàng hóa

| ID / spec | Cần chứng minh | Tiêu chí đạt và evidence cần có | Trạng thái |
| --- | --- | --- | --- |
| P01 / §5,21,58 | Player/camera trước Farm | WASD responsive; chạy mọi hướng, sát tường và đổi hướng liên tục trong 5 phút không jitter/clip/stuck. Camera follow 45–60° damping, không rung/xoay khó chịu, player và mục tiêu đọc được. PlayMode movement/collision + `002_player.png` và run notes. | NOT RUN |
| P02 / §5,7 | Gamepad-ready | Input Actions có keyboard và gamepad bindings; movement/interact bằng controller thực tế hoặc Input System simulated device được ghi rõ; đổi thiết bị không double-input/stuck. Kiểm diagonal speed và deadzone; ghi phần chưa kiểm controller thật. | NOT RUN |
| F01 / §3,4,41 | Farm/animals | Carrot/Tomato/Wheat harvest được; Cow→Milk, Chicken→Egg collect được; mỗi loại ≥5 chu kỳ tạo đúng đầu ra, không lấy lặp trong cooldown; bắt đầu manual. Automated time/quantity + tương tác thật + `003_farm.png`, `004_animals.png`. | NOT RUN |
| I01 / §9 | Items/recipes data-driven | Validate 100% config ID/reference/price/stack/category; không nhánh nghiệp vụ theo display name. Catalog và recipes đúng progression; thay quantity/time trong test config làm behavior đổi đúng. EditMode config tests. | NOT RUN |
| I02 / §10,32 | Carry/deposit/capacity | ≥20 transfer gồm rỗng/đầy/partial/đích không phù hợp; count không âm/vượt capacity; tổng hàng được bảo toàn trừ consume/sale có record. Harvest→carry→storage đạt bằng tương tác thật. EditMode + PlayMode + ledger trước/sau. | NOT RUN |
| I03 / §10 | Visual stacks | Carry/storage/shelf/machine output hiển thị đúng rỗng/ít/nhiều; kiểm 0,5,20 hoặc các ngưỡng tương ứng capacity. Visual phản hồi trong ≤1 frame sau event; nhìn scene phân biệt đầy/trống; không chỉ text UI. Ảnh cùng camera cho từng trạng thái + test event. | NOT RUN |
| U01 / §45,46 | HUD và feedback | Money, carried item, capacity, objective, upgrade price, shop status đồng bộ gameplay; đủ tiền/thiếu tiền rõ; final UI không dùng default Button chưa style. Kiểm 1080p, không text cắt hoặc che interaction; screenshot + visual checklist. | NOT RUN |

## Gate 2 — Automation, shop, customers và processing slice

| ID / spec | Cần chứng minh | Tiêu chí đạt và evidence cần có | Trạng thái |
| --- | --- | --- | --- |
| S01 / §4,41 | Farm shop đúng phạm vi | Có 2 shelves, storage, 1 checkout; player tự restock/checkout/collect money trước thuê người. ≥10 đơn manual từ hàng thật hoàn tất và thiếu hàng có feedback đúng. `005_shop.png` + sale traces. | NOT RUN |
| C01 / §12,13 | Customer mua thật | 10–15 khách concurrent đi Spawn→Enter→SelectProduct→GoToShelf→TakeProduct→FindCheckout→Queue→Pay→Exit bằng NavMesh; ≥50 sale hoàn tất; giỏ/kệ/queue/payment khớp. 0 sale rỗng, 0 credit trùng, 0 timer income khi không có purchase. PlayMode state/transaction traces + `006_customers.png`. | NOT RUN |
| C02 / §13,26 | Queue, path và pool | Chạy 15 phút, chặn một đường/để hết hàng/tạm ngừng checkout rồi phục hồi; 0 deadlock hoặc reservation mồ côi. Chỉ flag stuck khi >30 giây không tiến triển dù service/path sẵn sàng. ≥3 vòng tái dùng mỗi pooled customer không giữ cart/payment cũ. | NOT RUN |
| E01 / §11,12,32 | Money/purchase/upgrade | Money sau run = money đầu + sale hoàn tất − purchase hợp lệ. Kiểm thiếu/đủ đúng giá/vượt giá; ≥20 re-enter/duplicate attempts không double-charge/unlock; level/capacity đổi đúng; animation/VFX/sound/floating text phù hợp. EditMode ledger + PlayMode pad + screenshot. | NOT RUN |
| W01 / §3,14,41 | Farmer automation | Thuê Farmer bằng tiền thật; ≥10 harvest/delivery tự động, player không làm thay; sử dụng cùng inventory rules, dừng khi đầy và tiếp tục khi giải phóng; không duplicate harvest khi player tranh plot. Worker state traces + trực quan. | NOT RUN |
| W02 / §14,41 | Restocker/Cashier automation | Restocker chuyển hàng từ storage sang shelf, Cashier phục vụ queue; ≥20 đơn tự động không can thiệp player, không mất hàng/tiền; job có state và reservation rõ; 0 blocking runtime errors. `006_customers.png` + sale/inventory traces. | NOT RUN |
| M01 / §4,9,41 | Processing machine | Ít nhất 1 máy slice có input, processing time, output, capacity, motion, upgrade levels; ≥10 recipe cycles đúng số lượng; kiểm thiếu input/full output/upgrade/mid-cycle save; 0 output trùng. `007_processing.png` + recipe tests và ledger. | NOT RUN |
| V01 / §41,42 | Slice thật sự chơi được | Save sạch, 1 player, 3 crop items, Cow/Chicken, Milk/Egg chains, 1 máy, 2 kệ, 1 checkout, 10–15 khách, Farmer/Restocker/Cashier, pads/money/upgrades/save-load đều hoạt động. Chơi liên tục manual→first automation→shop→processing, không cheat/scene jump; ghi route, thời gian và bottlenecks. Tất cả row slice PASS, zero critical errors. | NOT RUN |

## Gate 3 — Một game liên tục và persistence cuối

| ID / spec | Cần chứng minh | Tiêu chí đạt và evidence cần có | Trạng thái |
| --- | --- | --- | --- |
| X01 / §2,4,48,53 | Supermarket progression | Unlock từ economy/progression đã có; nhiều shelves/categories/checkout lanes/storage/restock routes; demand làm khách chọn hàng thật; ≥30 sale ở khu mới từ supply chain khu trước. Chơi trong cùng save/map progression; `008_supermarket.png`, sale/input traces. | NOT RUN |
| X02 / §2,4,48,53 | Bakery/restaurant progression | Farm/processing cung cấp Flour/Egg/Milk hoặc recipe ingredients; bakery tạo Bread/Cake và bán thật. Nếu triển khai restaurant, kitchen→meal→waiter/customer→payment cũng phải có bằng chứng. ≥10 chu kỳ chế biến/bán tại khu mới; `009_bakery.png`, recipe/sale traces. | NOT RUN |
| X03 / §14 | Jobs khu sau | AnimalWorker/Processor/Cook/Waiter theo hệ thống được triển khai dùng shared WorkerAgent/state/reservation; ≥10 chu kỳ mỗi job hiện diện không duplicate/mất hàng và có blocked/recovery test. Ghi rõ job chưa triển khai, không suy ra từ tên class. | NOT RUN |
| SV01 / §15 | Save dữ liệu đúng | So sánh money, unlock IDs, levels, stage, employee unlocks, production progress và inventory cần bảo toàn trước/sau save-load; test ID ổn định qua đổi tên hiển thị/hierarchy; zero scene references trong payload. EditMode round-trip/schema/ID tests. | NOT RUN |
| SV02 / §15,53 | Quit/restart/load thật | ≥3 lần: sau unlock, giữa processing, khi checkout đang hoạt động; thoát executable, xác nhận process kết thúc, chạy lại load đúng progression/money/input/progress; không double-sale/output. Save test riêng, không xóa save người dùng. `restart-evidence.md` + payload diffs + Player.log review. | NOT RUN |

## Gate 4 — Art, audio và ba vòng polish

| ID / spec | Cần chứng minh | Tiêu chí đạt và evidence cần có | Trạng thái |
| --- | --- | --- | --- |
| A01 / §6,18–25,49 | Models/material/lighting | 0 pink/missing material/texture; normals/pivot/UV/scale đúng; important visible objects dùng model thật. Material Wood/Metal/Painted Metal/Plastic/Soil/Grass/Stone/Fabric/Food đọc được; cảnh sáng mềm, không bloom quá mức. Unity Inspector + ảnh game thật nhiều góc. | NOT RUN |
| A02 / §19–21 | Animation chính | Player Idle/Walk/Run/CarryIdle/CarryWalk/Pickup/Drop; Worker Idle/Walk/Work/Carry; Customer Idle/Walk/Shopping hoặc Carry/Checkout; Cow Idle/Walk/Eat; Chicken Idle/Walk/Peck. Pose test và transitions không pop/collapse; chạy 10 m + quay góc, foot sliding không gây hiểu sai movement. Clip/source checklist + visual evidence. | NOT RUN |
| A03 / §46,47 | Audio/juice | 9 nhóm âm pickup/drop/money/purchase/machine/customer/farm/animals/UI có audio hợp lệ, nghe được đúng event, không phát lặp ngoài ý muốn. Coin/item/purchase/UI/VFX vừa đủ; 10 lần trigger không stacking volume hoặc pool leak. Audio checklist + nguồn. | NOT RUN |
| PL1 / §43,44 | Polish 1: gameplay | Một clean-save run tới processing không cheat; ≥3 lần lặp đầu game, không softlock; ghi/điều chỉnh timing, move speed, production, money curve, customer speed, cost bằng số trước/sau. Play→Observe→Fix→Retest→Document, 0 critical issue. | NOT RUN |
| PL2 / §43,44 | Polish 2: visual | Models/animation/material/lighting/VFX/UI/camera qua checklist toàn map; 0 missing materials, 0 clipped HUD, 0 camera collision/jitter trong route 5 phút; screenshot trước/sau và retest state giao dịch không đổi. | NOT RUN |
| PL3 / §26,43,44 | Polish 3: QA/performance | 30 customers, workers + machines hoạt động; sau warm-up 2 phút đo standalone 1080p trong 10 phút: average ≥60 FPS, p95 frame-time ≤20 ms, p99 ≤33.3 ms; 0 sustained stall/queue deadlock/critical errors. Ghi draw calls, GC/memory, pool/LOD findings; profiler và build retest. | NOT RUN |

Performance thresholds ngoài mục tiêu 60 FPS/1080p là tiêu chí QA đề xuất. Ghi hardware thật; RTX 4060 pass chỉ chứng minh cấu hình đó, không tự suy rộng mọi PC tầm trung. Memory quan sát sau warm-up không tăng đơn điệu không giải thích được; khi tăng, tìm root cause trước kết luận. Không dùng editor FPS làm bằng chứng standalone performance.

## Gate 5 — Build, final route và bàn giao

| ID / spec | Cần chứng minh | Tiêu chí đạt và evidence cần có | Trạng thái |
| --- | --- | --- | --- |
| B01 / §31,34,52 | Compile/tests/build | Zero compiler errors; toàn bộ critical EditMode/PlayMode tests pass với XML không bị skip che lỗi; Windows x64 build thành công, `D:\APP\TYCOON\Build\Windows\TYCOON.exe` khởi chạy. Build report, exit code, executable hash, versions. | NOT RUN |
| B02 / §53 | Final route từ save sạch | Launch→move→farm→animals→carry→storage→shop→customers→money→purchase→workers→processing→supermarket→bakery→save→quit→restart→load, trong một progression liên tục, không cheats. Kiểm Console và Player.log sau run: zero critical/blocking runtime error; `010_final.png` + full route results. | NOT RUN |
| B03 / §35–39,54,57 | Documents và evidence khớp state | PLAN đủ CURRENT MILESTONE/GOAL/DONE/IN PROGRESS/NEXT/BLOCKERS/LAST TEST/LAST BUILD; DEVLOG append từng major task; audit/sources/architecture/QA/blockers/final report có trạng thái thật. FINAL_REPORT ghi versions/build/systems/inventory/processed models/rig/animations/sources/tests/issues/performance/remaining work. | NOT RUN |
| B04 / §33,34,40 | Không mang lỗi nặng sang milestone | Mỗi blocker nghiêm trọng giải quyết và retest trước milestone sau; cùng lỗi 2 lần sửa thất bại phải exact error/minimal repro/root cause/version/research/chosen fix. Commit ổn định scoped diff rồi push; record thất bại trung thực, không force push. | NOT RUN |
| B05 / AGENTS | Process/temp cleanup | Inventory toàn cây process do task tạo với PID/ParentPID/command line; dừng nhẹ nhàng process hết cần, xác nhận exit rồi dọn đúng temporary paths. Process giữ lại có PID, purpose, stop condition; không tự tạo backup/archive hoặc xóa source/save/deliverable. Cleanup evidence trong final report. | NOT RUN |

## Run record cần điền sau mỗi major task

```text
Run ID / timestamp:
Milestone / requirement IDs:
Commit / dirty files:
Unity / URP / packages / scene or build:
Hardware / GPU backend / resolution / quality:
Save profile / initial conditions:
A compile/static: command/tool, evidence, PASS/FAIL/BLOCKED
B EditMode/PlayMode: executed/skipped/failed, XML, expected vs actual
C actual interaction: route/actions, screenshots, expected vs actual
Quantitative measurements:
Bugs / exact errors / root cause / fix:
Retest evidence:
Build path / result:
Processes kept or stopped / temporary files removed or retained:
Next required gate:
```

Hoàn thành toàn bộ chỉ khi evidence hiện tại chứng minh mọi yêu cầu liên quan, cả progression khu sau và Windows restart/load. `NOT RUN`, thiếu evidence, ảnh không xuất phát từ game thật hoặc kết quả không cùng build đều không đủ để đánh dấu DONE.

## 2026-10-01 — verified component runs

- Unity 6000.6.3f1, CPU batch/Null Device; compile clean.
- core-edit-r3.xml: 53 passed, 0 failed/skipped; foundation/commerce/production tests.
- core-play-r1.xml: 33 passed, 0 failed/skipped; player, NavMesh customer/pooling and interaction/progression tests.
- No Console errors/exceptions/assertions reported for either run.
- This proves the component scenarios covered by those suites. Broad scene, visual, animation, build and full-game gates above remain unpassed.
- Portable case-level evidence: Documentation/TestEvidence/core-components-2026-10-01.json.

## 2026-10-02 persistence milestone

- 93 passing tests: 53 EditMode and 40 PlayMode under Unity 6000.6.3f1 / Null Device.
- Added six save coordination tests and a live customer queue/save projection test.
- Verified consumed recipe batches, worker cargo, crop timers, player pose and progression
  restoration; receipts collect exactly once; invalid worlds and nonfinite data are rejected.
- Portable evidence: `Documentation/TestEvidence/persistence-2026-10-02.json`.
- Not evidence of full-world restart, animation appearance, graphics, frame rate or release readiness.
