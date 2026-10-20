package com.arvirtuallab.text;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.Html;
import android.text.Layout;
import android.text.StaticLayout;
import android.text.TextPaint;

/**
 * Draws text with Android's own text engine, which shapes Indic scripts such as Telugu correctly
 * (TextMeshPro does not). Called from NativeText.cs.
 */
public final class NativeTextRenderer {
    private NativeTextRenderer() {}

    /**
     * @param html       text, with Android HTML for colour / bold / italic / sub / sup
     * @param sizePx     font size in pixels (the largest size when auto-sizing)
     * @param minSizePx  smallest size to shrink to so the text fits maxHeightPx
     * @param widthPx    wrapping width; ignored when wrap is false
     * @param maxHeightPx shrink until the text fits this height; 0 = never shrink
     * @param hAlign     0 left, 1 centre, 2 right
     * @return 4 bytes width, 4 bytes height (big-endian), then straight-alpha RGBA pixels, top row first
     */
    public static byte[] render(String html, float sizePx, float minSizePx, int argb, int widthPx, int maxHeightPx,
                                int hAlign, boolean bold, boolean italic, boolean wrap) {
        TextPaint paint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
        paint.setColor(argb);
        int style = (bold ? Typeface.BOLD : 0) | (italic ? Typeface.ITALIC : 0);
        paint.setTypeface(Typeface.create(Typeface.DEFAULT, style));

        CharSequence text = Html.fromHtml(html, Html.FROM_HTML_MODE_LEGACY);
        Layout.Alignment align = hAlign == 1 ? Layout.Alignment.ALIGN_CENTER
                : hAlign == 2 ? Layout.Alignment.ALIGN_OPPOSITE : Layout.Alignment.ALIGN_NORMAL;

        float size = Math.max(1f, sizePx);
        StaticLayout layout;
        while (true) {
            paint.setTextSize(size);
            int width = wrap ? Math.max(1, widthPx)
                             : Math.max(1, (int) Math.ceil(Layout.getDesiredWidth(text, paint)) + 2);
            layout = StaticLayout.Builder.obtain(text, 0, text.length(), paint, width)
                    .setAlignment(align)
                    .setIncludePad(true)
                    .build();
            if (maxHeightPx <= 0 || layout.getHeight() <= maxHeightPx || size <= minSizePx) break;
            size = Math.max(minSizePx, size - Math.max(1f, size * 0.06f));
        }

        int w = layout.getWidth();
        int h = Math.max(1, layout.getHeight());
        Bitmap bitmap = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888);
        layout.draw(new Canvas(bitmap));

        int[] argbPixels = new int[w * h];
        bitmap.getPixels(argbPixels, 0, w, 0, 0, w, h);        // straight (not premultiplied) ARGB
        bitmap.recycle();

        byte[] out = new byte[8 + w * h * 4];
        writeInt(out, 0, w);
        writeInt(out, 4, h);
        int o = 8;
        for (int p : argbPixels) {
            out[o++] = (byte) (p >> 16);
            out[o++] = (byte) (p >> 8);
            out[o++] = (byte) p;
            out[o++] = (byte) (p >>> 24);
        }
        return out;
    }

    private static void writeInt(byte[] b, int at, int v) {
        b[at] = (byte) (v >>> 24);
        b[at + 1] = (byte) (v >>> 16);
        b[at + 2] = (byte) (v >>> 8);
        b[at + 3] = (byte) v;
    }
}
