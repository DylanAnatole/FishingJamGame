# Kế hoạch level design: Fishing Jam

## 1. Nguyên tắc

1. **Mỗi màn chỉ có một thứ mới.** Thứ đó xuất hiện ở dạng dễ trước, rồi mới kết hợp với thứ cũ.
2. **Không giải thích bằng chữ.** Màn đầu tiên của một luật phải tự dạy được luật đó: ít cá, nhiều giờ, khó làm sai.
3. **Độ khó răng cưa.** Khó dần trong 4 màn, đến màn đỉnh (boss), rồi 1 màn dễ để thở.
4. **Áp lực chính là thời gian.** Màn dài 1,5–3 phút. Người chơi khéo còn khoảng 40–50% đồng hồ (đủ 2 sao thời gian).
5. **Chơi 25% thả câu, 75% kéo cá.** Độ khó chủ yếu nằm ở trận kéo; pha thả câu thử thách bằng việc chọn mồi và né.

## 2. Các núm vặn độ khó đang có

| Núm | Ở đâu (Inspector) | Vặn lên thì |
|---|---|---|
| Thời gian màn | `Level.time` | ít giờ hơn thì gắt hơn (núm mạnh nhất) |
| Cá cần bắt | `Level.need` | nhiều con hơn hoặc loài khó hơn thì màn dài và khó hơn |
| Cá trong hồ | `Level.pond` | thêm cá không cần (cá mồi nhử) thì dễ dính nhầm (−10 giây) |
| Mồi được chọn | `Level.baits` | mồi nào cũng hút kèm cá không cần thì phải tính chọn mồi |
| Cành cây | `Level.snags` | nhiều, dài, nhanh hơn thì khó lái móc |
| Nhiệm vụ phụ | `Level.quest`, `questTarget` | mục tiêu cho người giỏi (sao thứ 3) |
| Sức cá | `Species.hp` | trận kéo dài hơn |
| Tốc độ tụt GIỮ | `Species.drain` | dễ đứt dây hơn |
| Cú lao | `Species.lunges`, `yank` | giật GIỮ mỗi lần cá giằng |
| Chiêu cá | `Species.skill` | mực che ô, nóc khóa ô, cá mập ném rong |

Bàn ghép luôn có 5 ô giống nhau ở mọi màn. **Không dùng bàn để tăng độ khó.** Bàn là thứ người chơi đã quen tay.

### Bảng cá hiện có

| Cá | Sao | Trận kéo (khéo) | Đặc điểm | Mồi hút |
|---|---|---|---|---|
| Chép | ★ | ~10 giây | hiền | giun, ngô |
| Nóc | ★★ | ~12 giây | xù gai khóa 3 ô | giun, tôm |
| Rô phi | ★★ | ~12 giây | hiền | giun, ngô |
| Thu | ★★★ | ~18 giây | lao, bơi rất nhanh (khó móc) | tôm, cá con |
| Ngừ | ★★★★ | ~20 giây | lao mạnh | cá con, mồi giả |
| Mực | ★★★★ | ~17 giây | phun mực che 4 ô | tôm, mồi giả |
| Cá mập | ★★★★★ | ~23 giây | lao, ném rong | cá con, mồi giả |

## 3. Thứ tự giới thiệu luật

| # | Thứ mới | Màn dạy nó | Cách dạy không cần chữ |
|---|---|---|---|
| 1 | Vuốt ghép 3, hai thanh GIỮ/CÂU | 1 | chỉ có chép, 1 mồi, nhiều giờ |
| 2 | Vợt (CÂU ≥70%) | 1–2 | vạch 70% và icon Vợt trên thanh CÂU, ô Vợt sáng lên khi qua vạch |
| 3 | Chọn mồi, cá mồi nhử | 2 | 2 mồi: một mồi hút cả nóc, một mồi không |
| 4 | Ô đặc biệt, combo | 3 | nhiệm vụ phụ "tạo 3 ô đặc biệt" |
| 5 | Cá lao (giật GIỮ) | 4 | cá ngừ, chỉ 1 con |
| 6 | Cành cây | 5 | 2 cành chậm, hồ ít cá |
| 7 | Mực che ô | 6 | |
| 8 | Cá mập ném rong (boss) | 7 | |
| 9 | **Nóc khóa ô** | **chưa có** | hiện nóc chỉ là cá mồi nhử, chưa bao giờ phải kéo |

**Lỗ hổng:** chiêu xù gai của nóc chưa được dạy. Đề xuất cho màn 8 là màn "nóc là cá cần bắt".

## 4. Khung 30 màn (3 hồ × 10 màn)

Mỗi hồ có 10 màn: màn 5 là đỉnh nhỏ, màn 10 là boss, màn 6 là màn thở.

### Hồ 1: Bến làng (học luật), màn 1–10

| Màn | Trọng tâm | Cần bắt | Mồi | Cá mồi nhử | Cành | Nhiệm vụ phụ |
|---|---|---|---|---|---|---|
| 1 | vuốt, 2 thanh | chép 2 | giun | – | – | không hụt |
| 2 | chọn mồi | chép 3 | giun, ngô | nóc | – | không hụt |
| 3 | ô đặc biệt | rô phi 2, chép 2 | giun, ngô, tôm | nóc | – | 3 ô đặc biệt |
| 4 | cá lao | ngừ 1, rô phi 1, chép 1 | ngô, cá con, mồi giả | thu, mực | – | combo 3 |
| 5 | **đỉnh nhỏ**: cành cây | thu 3 | tôm, cá con | nóc, ngừ | 2 | 4 ô đặc biệt |
| 6 | thở: mực | mực 2, thu 1 | giun, tôm, mồi giả | chép, nóc, ngừ | – | combo 4 |
| 7 | nóc khóa ô *(mới)* | nóc 2, chép 1 | giun, tôm | rô phi, thu | – | không hụt |
| 8 | trộn: cành + mồi nhử | rô phi 2, nóc 1 | giun, ngô | chép nhiều | 1 | 5 ô đặc biệt |
| 9 | trộn: lao + mực | ngừ 1, mực 1 | cá con, mồi giả | thu, cá mập | 1 | combo 4 |
| 10 | **boss cá mập** | cá mập 1, mực 1, thu 1 | tôm, cá con, mồi giả | nóc, ngừ | 2 | không hụt |

> Màn 1–7 hiện có trong game là bản nháp của màn 1–6 và màn 10 ở đây. Khi làm thêm thì chèn màn 7–9 vào giữa.

### Hồ 2: Hồ sen (kết hợp), màn 11–20

Không có luật mới, chỉ **ghép luật cũ**: 2 chiêu cá trong một màn, nhiều cá mồi nhử hơn, mồi nào cũng có cá nhử.

| Màn | Ý tưởng |
|---|---|
| 11 | thở: chép, rô phi với ít giờ hơn hồ 1 |
| 12 | 3 mồi, mồi nào cũng hút ít nhất 1 cá nhử |
| 13 | nóc và mực cùng màn (gai và mực trên một bàn dùng chung) |
| 14 | 3 cành nhanh, cần thu 2 |
| 15 | **đỉnh**: ngừ 2, lao liên tục |
| 16 | thở |
| 17 | cần 4 loài khác nhau, phải đổi mồi giữa màn |
| 18 | ít giờ, toàn cá ★1–2: thi tốc độ |
| 19 | mực 2 và cá mập 1 |
| 20 | **boss**: cá mập 2 |

### Hồ 3: Biển (thử thách), màn 21–30

Cùng các núm như trên nhưng giờ gắt hơn khoảng 10%, nhiệm vụ phụ khó hơn (combo 5, 6 ô đặc biệt). Màn 30 là boss cuối: cá mập 2 cộng mực 2.

> Chỉ có 7 loài nên tới hồ 3 sẽ bắt đầu lặp. Đây là chỗ hợp lý để sau này thêm loài mới, mỗi loài một chiêu riêng. **Chưa làm bây giờ.**

## 5. Quy trình làm một màn

1. **Nháp** trong Inspector của `FishingGame → levels`: cá cần bắt, hồ, mồi, cành, nhiệm vụ phụ.
2. **Kiểm tra mồi:** mỗi loài cần bắt phải có ít nhất một mồi hút nó. Nên có thêm một mồi hút kèm cá nhử, để việc chọn mồi có nghĩa.
3. **Chạy mô phỏng:** menu *FishingJam → Mô phỏng cả màn*. Lấy cột "Giờ gợi ý" làm thời gian màn.
4. **Chơi thử** 2–3 lần. Mục tiêu:

   | Loại màn | Khéo còn lại | Vụng kịp giờ |
   |---|---|---|
   | dạy luật / thở | ≥ 55% | ≥ 90% |
   | thường | 40–50% | 70–80% |
   | đỉnh / boss | 30–40% | 50–60% |

5. **Chỉnh** theo thứ tự: thời gian màn trước, rồi cá cần bắt, cuối cùng mới đến chỉ số cá. Chỉ số cá ảnh hưởng mọi màn có loài đó.

## 6. Việc cần làm tiếp

- [x] Đã thêm màn 7–9 của hồ 1 và dời boss xuống màn 10 (thời gian: 135 / 125 / 95 / 130 giây).
- [ ] Chơi thử thật hồ 1 để lấy số "vụng kịp giờ" thật, thay cho bot.
- [ ] Mô phỏng hiện chạy 15 lượt mỗi màn nên còn dao động. Khi chốt số thì chạy 50 lượt từ menu.
- [ ] Thay hình rô phi và mực (xem `ArtSource/ASSETS_NEEDED.md`).
