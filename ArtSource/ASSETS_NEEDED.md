# Danh sách hình cần tìm cho Fishing Jam

Game nạp mọi hình từ `Assets/Resources/Art/` theo **tên file**. Chỉ cần có file PNG đúng tên là game tự dùng, không phải sửa code.
Bạn không cần đổi tên: cứ bỏ pack vào `D:\luyentayunityxgame\asset\` rồi báo mình, mình sẽ cắt, đổi tên, chỉnh kích thước và lật hướng.

**Yêu cầu chung:** PNG nền trong suốt. Cả bộ nên **cùng một phong cách**: hoặc toàn pixel art, hoặc toàn vẽ phẳng, đừng trộn.
Hiện game đang trộn pixel art với hình vẽ phẳng, nên chỗ đó trông lệch tông nhất.

---

## Ưu tiên 1: bàn ghép (người chơi nhìn nhiều nhất)

| Tên file | Là gì | Hiện đang dùng | Ghi chú |
|---|---|---|---|
| `tile_giat` | Giật cần (kéo mạnh) | Icons_03 (pixel) | icon vuông, rõ ở cỡ nhỏ khoảng 100 px |
| `tile_thu` | Thu dây (cuộn dây) | Tiny Coast rod (pixel) | |
| `tile_noi` | Nới dây | Icons_01 (pixel) | |
| `tile_ghim` | Ghìm cần | Fish-rod (pixel) | |
| `tile_vot` | Vợt (kết liễu) | **tự vẽ, lệch tông** | thiếu nhất, nên tìm trước |
| `tile_rong` | Rong (chướng ngại cá mập ném) | Grass1 (pixel) | |
| `board_bg` | Khung gỗ quanh bàn 7×7 | tự vẽ | nên co giãn được (9-slice) |

5 ô hành động cần **khác nhau rõ về hình và màu**: người chơi phân biệt bằng mắt, không đọc chữ.
Một bộ icon "đồ câu cá" hoặc "câu cá RPG" gồm cần, cuộn dây, vợt, mồi là hợp nhất.

## Ưu tiên 2: cá (7 loài, nhìn ngang)

| Tên file | Loài | Hiện đang dùng |
|---|---|---|
| `fish_chep` | Cá chép ★ | Catch/3 (pixel) |
| `fish_noc` | Cá nóc ★★ | Catch/2 |
| `fish_rophi` | Cá rô phi ★★ | Catch/1 (rất nhỏ, nên thay) |
| `fish_cathu` | Cá thu ★★★ | Catch/4 |
| `fish_ngu` | Cá ngừ ★★★★ | Catch/7 |
| `fish_muc` | Mực ★★★★ | **cá đèn lồng thay tạm**, nên tìm mực thật |
| `fish_camap` | Cá mập ★★★★★ (boss) | Catch/6 |

Cá nhìn ngang, quay trái hay phải đều được. Nếu cá có 2 khung bơi thì càng tốt.

## Ưu tiên 3: hồ và cảnh

| Tên file | Là gì | Hiện đang dùng |
|---|---|---|
| `sky` | Dải trời, mây, đồi ngay trên mặt nước (1080×160) | tự vẽ |
| `wave` | Đường sóng mặt nước (1080×40) | tự vẽ |
| `seabed` | Đáy hồ (đá, rong) | tự vẽ |
| `panel_water` | Khung nước sau con cá trong trận kéo | tự vẽ |
| `branch` | Cành cây trôi (chướng ngại khi thả câu) | tự vẽ |
| `hook` | Lưỡi câu | tự vẽ |

## Ưu tiên 4: nhân vật và trang chủ (đã khá ổn)

| Tên file | Là gì | Hiện đang dùng |
|---|---|---|
| `boat` | Người câu cầm cần đứng trên thuyền (pha thả câu) | Fisherman + Boat (pixel) |
| `boat_idle_0..3` | Người câu đứng nghỉ, 4 khung (trang chủ) | Fisherman_idle |
| `boat_row_0..3` | Người chèo thuyền, 4 khung (loading) | Fisherman_row |
| `hut` | Nhà sàn bên hồ (trang chủ) | Fishing_hut |

## Ưu tiên 5: giao diện

| Tên file | Là gì |
|---|---|
| `button` | Nền nút bo góc (co giãn 9-slice, viền khoảng 24 px) |
| `icon_shuffle` | Icon nút xáo bàn |

Hai thanh GIỮ/CÂU, các hộp thoại và chữ đang vẽ bằng màu trơn. Nếu tìm được bộ UI cùng phong cách (thanh tiến độ, khung hộp thoại) thì mình lắp thêm được.

---

### Font
Game đang dùng **Liberation Sans** (file `Assets/Resources/Fonts/LiberationSans.ttf`, giấy phép OFL), có đủ dấu tiếng Việt nhưng **không có bản đậm**, nên tiêu đề trông hơi mảnh.
Nếu tìm được một font miễn phí (OFL) có đủ tiếng Việt **và có bản Bold**, ví dụ Be Vietnam Pro, Nunito hoặc Baloo 2, thì thay vào là chữ đẹp và đậm hơn.

### Hình có trong thư mục nhưng game không dùng
`tile_banhmi`, `tile_cacon`, `tile_cuon`, `tile_giun`, `tile_moigia`, `tile_ngo`, `tile_tom` (icon mồi cũ),
`tray_bg`, `icon_undo`, `icon_pull`, `coin` (từ khay 7 ô và thanh xu cũ), `global`, `global_free_recoloured` (sprite sheet gốc của pack fishing_free).
Không ảnh hưởng gì tới game. Nếu muốn mình xóa cho gọn thì báo nhé.
