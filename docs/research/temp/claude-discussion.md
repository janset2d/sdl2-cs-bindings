Tartışmayı okudum. Teknik iskelet büyük ölçüde doğru, ama bir yerde önceki Claude kendi "orta yol" önerisiyle senin asıl hedefin arasında sıkışmış ve sonuçta tavsiye ettiği çözüm bence senin use case'inde aktif olarak kötü bir fikir. Nerede katılıyorum, nerede katılmıyorum tek tek:
Doğru olan kısımlar:
RPATH vs SONAME/DT_NEEDED ayrımı net doğru. RPATH "nerede", SONAME/NEEDED "neyi" sorusunu cevaplar — bu disiplin tartışma boyunca korunmuş, iyi. Triplet'in policy layer olması ve binary identity'yi değiştirmemesi de doğru çerçeve. Senin hybrid triplet'in zaten olması gereken yerde duruyor: linkage preference + flag'ler. patchelf ve install_name_tool'un bu işin asıl aletleri olduğu da tartışmasız.
Katılmadığım yerler:
1. "SONAME-level canonical name" orta yolu (libSDL2.so.0) senin bağlamın için yanlış tavsiye.
Önceki cevaplarda Debian policy'sine atıfla "tam unversioned biraz cursed, SONAME seviyesinde kal" dedi. Ama sen distro maintainer değilsin; self-contained NuGet runtime asset paketliyorsun. Debian policy'nin varlık sebebi sistem geneli shared library governance — birden fazla uygulamanın aynı libSDL2'ye farklı major version beklentileriyle ulaşabilmesi. Senin NuGet paketinde böyle bir paylaşım hiç yok; her consumer kendi runtimes/<rid>/native klasöründen yükleyecek. SONAME versioning'in bu bağlamda hiçbir pratik faydası yok, sadece DllImport tarafında [DllImport("SDL2")] ile libSDL2.so.0 arasındaki eşleşmeyi sıkıntılı hale getirir.
Net söyleyeyim: hedefin tam unversioned olmalı, hem dosya adında hem SONAME/install_name'da. Microsoft'un kendi native NuGet örneği de zaten bu.
2. Normalization'ı overlay port'a koyma önerisiyle mimari olarak ayrışıyorum.
Önceki tartışma "her SDL family port için binary normalization overlay yaz" dedi. Teknik olarak çalışır ama bence yanlış kata koyuyor. Sebeplerim:
Normalization senin için vcpkg concern'ü değil, NuGet packaging concern'ü. Mevcut overlay'lerinin README'deki gerekçeleri meşru — sdl2-gfx Unix visibility, sdl2-mixer codec policy gibi gerçek upstream/packaging mismatch'ler. "NuGet için unversioned istiyorum" ise upstream'de bug değil, senin distribution layer'ının tercihi. Bunu overlay'e sokarsan her upstream bump'ta merge conflict ve patch bitrot riski alırsın, üstelik vcpkg'nin kendi test step'inden çıkacak hali de belirsizleşir.
Benim önerim: normalization'ı Cake pipeline'ına koy, vcpkg export sonrası ayrı bir step olarak.
Akış şu olsun:

vcpkg hybrid triplet ile upstream-standard output üretir — versioned dosyalar, symlink chain'ler, orijinal SONAME'ler. Bu hali kırma.
Cake "harvest" step vcpkg installed tree'den ilgili binary'leri toplar.
"Normalize" step:

Linux: symlink chain'i flatten et (gerçek dosyayı tut, linkleri sil), patchelf --set-soname ile unversioned yap, her dependent'da --replace-needed ile NEEDED kayıtlarını yeni isimlere çek, --set-rpath '$ORIGIN'.
macOS: symlink'leri sil, install_name_tool -id @rpath/libSDL2.dylib, dependent'larda -change ile tüm reference'ları yeni isimlere çek, -add_rpath @loader_path.
Windows: zaten sorun yok.


"Audit" step (HybridStaticValidator): readelf -d / otool -L / dumpbin /dependents çıktılarını parse et, whitelist dışında hiçbir dependency kalmadığını assert et.
NuGet pack: normalize edilmiş ve audit'ten geçmiş binary'leri runtimes/<rid>/native altına koy.

Avantajları: vcpkg saf kalır, upstream takip edilir; logic tek yerde, C#'ta, debug edilebilir; rule'lar explicit; upstream bump'larda overlay patch conflict'i yok; CI her RID için aynı pipeline'ı koşar. Trade-off: vcpkg installed tree "paketlenecek hali" değil "ham hali" oluyor, ama bu zaten vcpkg-native doğrulama için daha sağlıklı.
Overlay port'un hâlâ gerekeceği yerler:
Post-processing'le çözülmeyen source-level dertler. Örneğin upstream'in SDL2Config.cmake gibi export dosyalarında yanlış dependency ismi üretmesi (sen vcpkg consumer gibi davranan başka bir build'den tüketeceksen önemli), macOS'ta .framework bundle layout'u, ya da CMake export symbol logic'inde düzeltilmesi gereken bir şey. Ama sırf dosya adı/SONAME için overlay port yazma.
Bir de uyarı:
Triplet'teki -fvisibility=hidden iyi başlangıç ama satellite içine gömülü static libpng/freetype/jpeg sembollerinin sızmasını tek başına garanti etmez. Static lib'lerin kendi .o'larında explicit __attribute__((visibility("default"))) varsa bu flag etki etmez. O yüzden HybridStaticValidator sadece dependency whitelist değil, exported symbol whitelist de kontrol etmeli — Linux'ta nm -D --defined-only libSDL2_image.so, macOS'ta nm -gU libSDL2_image.dylib. Export edilen sembollerde SDL API prefix'i dışında bir şey (png_, jpeg_, FT_* vb.) görünürse PoC kırmızı alarm.
Net karar özeti:

Hedef: tam unversioned, hem paket yüzeyinde hem binary metadata'sında. "Canonical versioned" orta yolu kabul etme.
Hybrid triplet aynen kalsın, iyi iş.
Overlay port'lar sadece gerçek upstream/policy mismatch için. Normalization overlay'i yazma.
Normalization + audit, Cake pipeline'ında post-vcpkg step olsun.
HybridStaticValidator'ü hem dependency whitelist hem sembol export whitelist kapsayacak şekilde genişlet.

İstersen bir sonraki mesajda PoC fazın için Cake pipeline'ının normalize+audit step'lerine C# scaffold'u ya da concrete patchelf/install_name_tool komut akışını çıkarayım.