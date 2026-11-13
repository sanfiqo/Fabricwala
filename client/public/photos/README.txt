Product photos
==============

Put real photos here, named <productId>-<n>.jpg
  1-1.jpg   cover photo of product 1
  1-2.jpg   second photo of product 1
  2-1.jpg   cover photo of product 2

Sizes:  800 x 800 pixels (square), JPEG quality about 80, under 150 KB each.
        The item centred with some empty space around it, plain light background.
        At least 1 photo per product, up to 8.

After adding files run:   npm run photos
(it writes manifest.json, so the app only asks for files that exist).

Photos from the server are used for any product without a file here.
The photo slot has a fixed size, so the screens do not move when photos arrive.
