"""把 FZ logo 转成 Windows 多尺寸 .ico

用法: python make_icon.py <源图> <输出.ico>

做的事:
  1. 裁掉四周接近纯白的留白，让 logo 在小尺寸下也够大
  2. 切成正方形
  3. 生成 16/24/32/48/64/128/256 七个尺寸打包进 .ico
     —— 只塞一个尺寸的话，任务栏和资源管理器里会是糊的
"""
import sys
from PIL import Image


def trim_bg(im, tol=8):
    """裁掉四周接近背景色的边框"""
    im = im.convert("RGB")
    w, h = im.size
    px = im.load()
    bg = px[2, 2]

    def is_bg(c):
        return (abs(c[0] - bg[0]) <= tol and abs(c[1] - bg[1]) <= tol
                and abs(c[2] - bg[2]) <= tol)

    step_y = max(1, h // 64)
    step_x = max(1, w // 64)
    left, right, top, bottom = 0, w - 1, 0, h - 1
    while left < w - 1 and all(is_bg(px[left, y]) for y in range(0, h, step_y)):
        left += 1
    while right > 0 and all(is_bg(px[right, y]) for y in range(0, h, step_y)):
        right -= 1
    while top < h - 1 and all(is_bg(px[x, top]) for x in range(0, w, step_x)):
        top += 1
    while bottom > 0 and all(is_bg(px[x, bottom]) for x in range(0, w, step_x)):
        bottom -= 1
    return im.crop((left, top, right + 1, bottom + 1))


def main():
    src, dst = sys.argv[1], sys.argv[2]
    im = Image.open(src)
    print("源图: %s  %s  %s" % (src, im.size, im.mode))

    if im.mode == "RGBA" and im.getextrema()[3][0] < 255:
        im = im.convert("RGBA")
        print("有透明通道，跳过裁边")
    else:
        im = trim_bg(im).convert("RGBA")
        print("裁边后: %s" % (im.size,))

    w, h = im.size
    s = max(w, h)
    square = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    square.paste(im, ((s - w) // 2, (s - h) // 2), im)
    print("正方形: %s" % (square.size,))

    sizes = [256, 128, 64, 48, 32, 24, 16]
    square.save(dst, format="ICO", sizes=[(n, n) for n in sizes])
    print("已写出: %s" % dst)

    square.resize((256, 256), Image.LANCZOS).save(dst.replace(".ico", ".png"))
    print("已写出 PNG 预览")


main()
