# Product photos

## What happens on upload (`POST /api/v1/seller/products/{id}/media`)

1. The file must be a JPEG, PNG or WebP (checked from its first bytes) and up to 5 MB, and the product may have 8 photos.
2. It is then read as an image (`photo_unreadable`, 400, when it cannot be, or when it is larger than 50 megapixels). The camera orientation is applied, so a phone photo stored sideways is turned upright.
3. Three square WebP copies are made: **main** 800 x 800, **card** 400 x 400, **thumb** 120 x 120. The photo is fitted inside the square on a white background: never cut and never enlarged (a small photo sits in the middle of the white square).
4. A **lqip** is made: a 20 pixel wide JPEG (1 KB or less) as a `data:image/jpeg;base64,...` text, for a blurred placeholder while the real photo loads.
5. The original and the three copies are stored privately. The copies and the lqip are remembered on the photo (`MainKey`, `CardKey`, `ThumbKey`, `Lqip`).

The image work uses SkiaSharp (MIT licence), with its Linux native files in the package.

## Addresses

`/api/v1/products/{slug}/media/{id}` is the original. Add `?size=main`, `?size=card` or `?size=thumb` for a WebP copy (`size=` anything else is 400). A photo without copies (uploaded before this change, or added by web address) gives the original for every size, so old photos keep working.

| Where | `imageUrl` is | Also |
| --- | --- | --- |
| Product list, home, seller product list | card copy | `imageSrcSet` (list and product page), `imageLqip` |
| Product page | main copy | `imageSrcSet`, `imageLqip`, and each entry of `media` has `url` (main), `srcSet`, `lqip` |
| Cart item | thumb copy | `imageLqip` |
| Seller product detail, upload response | main copy | `srcSet`, `lqip` on every photo |

`srcSet` is `"<card address> 400w, <main address> 800w"`, ready for an `<img srcset>`. It and `lqip` are null for a photo without copies.

## Not done

- Order items have no photo: an order keeps no snapshot of the product picture.
- Deleting a photo removes its row; the stored files stay (there is no delete in the storage interface yet).
- Copies are made while the seller waits for the upload to finish (a few hundred milliseconds for a phone photo). A background queue is not needed at this size.
- No cache headers yet on the photo routes (task F9.5).
