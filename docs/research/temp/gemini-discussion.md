Aşağıdaki tartışmayı oku ve kendi fikrini belirt



vcpkg nin ürettiği native libler linux ve macos'de symlink ve versioned olarak çıkıyor, bunu symlinksiz tek bir native .so ve .dylib çıkarttırmanın port veya triplet overlay ile bir yolu var mı? SDL satelite kütüphaneleri için diyorum. Ama böyle bir durumda SLD2_Image Rpath hala versiyonlu ya mı bakar. EN kötü ihtimalle RPATH ile unversioned dosyalara bakma override'ı yapılır mı

Bunu iki eksende bakıyorum: vcpkg tarafında “çıktıyı symlinksiz üretmek mümkün mü”, bir de ELF/mach-o tarafında “diğer kütüphaneler seni isimle nasıl buluyor”. Muhtemelen mesele sadece dosya adını değiştirmek değil, içteki SONAME/install_name ve bağımlılık kayıtlarını da hizalamak.

İlk bulgu: tek başına RPATH bu işi “tam çözüm” yapmıyor; loader’ın aradığı isim çoğu zaman binary içine gömülü NEEDED/SONAME ya da install_name. Yani dosyayı yeniden adlandırırsan ama iç metadata eski kalırsa, loader eski ismi aramaya devam eder.

Thought for 1m 1s

Evet, overlay port ile yapılabilir, ama triplet tek başına yetmez. Triplet sana esasen “shared mı static mi” gibi build modunu verir; dosya adlandırma, SONAME/install_name ve symlink davranışı için genelde port patch’i gerekir. vcpkg’nin resmi dokümanı da Linux/macOS’ta dynamic build için custom overlay triplet yaklaşımını öneriyor, ama isimlendirme/symlink politikasını ayrı bir özellik olarak vermiyor.

Asıl kritik nokta şu:

RPATH arama yolu belirler, ama hangi dosya adının aranacağını belirlemez.

Linux’ta loader, binary içine gömülü bağımlılık kayıtlarına göre .so dosyalarını bulur; Debian policy de tipik yapının “link-time için bare .so symlink’i, runtime için SONAME symlink’i” olduğunu söylüyor. Ama aynı metin şunu da söylüyor: bu bir konvansiyon, zorunluluk değil; bazı kütüphaneler doğrudan SONAME’i gerçek dosya adı olarak kullanır ve bu yüzden symlink gerekmez.

Yani Linux tarafında teorik olarak bu mümkün:



gerçek dosya: libSDL2_image.so.0

symlink yok

ELF içindeki SONAME = libSDL2_image.so.0

Bu durumda runtime çalışır. Ama başka bir shared library bunu link ederken genelde linker libSDL2_image.so arar; Debian’ın anlattığı “bare .so symlink” tam da bu yüzden vardır.

Senin sorunun özelinde:



1) “vcpkg bunu symlinksiz tek .so / .dylib üretsin” mümkün mü?

Evet, ama en sağlam yol overlay port patch’i.

Genelde yapacağın şey:

Linux

Port’un CMake/autotools ayarlarını patch’leyip:



gerçek çıktı dosya adını istediğin ada çekmek

SONAME’i ona göre ayarlamak

install step’te symlink creation’ı kapatmak / silmek

Eğer CMake target kullanıyorsa çoğu zaman bunlar çevresinde dönersin:



OUTPUT_NAME

VERSION

SOVERSION

SDL tarafında da versioning aktif. Örneğin SDL_image’ın CMake’i shared build için açıkça SOVERSION ve VERSION set ediyor; Apple’da ayrıca Mach-O version alanlarını da ayarlıyor.

Yani patch’te ya:



VERSION/SOVERSION mantığını değiştirirsin,

ya da install sonrası patchelf --set-soname ... ve dosya rename yaparsın.

macOS

Burada mesele symlink’ten çok install_name. Apple’ın dokümanına göre run-path dependent library kullanacaksan dylib’in install name’i @rpath/<libname>.dylib biçiminde olur, executable da kendi RPATH listesiyle bunu çözer.

Dolayısıyla macOS’te güzel yol şudur:



gerçek dosya: libSDL2_image.dylib

install name: @rpath/libSDL2_image.dylib

executable / host dylib: uygun LC_RPATH

Bu, versioned filename ihtiyacını bayağı azaltır. Ama bunu da port patch’iyle ya da post-build install_name_tool -id / -change ile yaparsın. Apple’ın run-path açıklaması bunu destekliyor.



2) “SDL2_image hâlâ versioned ada mı bakar?”

Muhtemelen evet, build nasıl yapıldıysa ona bakar.

Burada kritik olan şey DT_NEEDED (Linux) veya dependent install name (macOS). RPATH sadece nerede arayacağını söyler, neyi arayacağını değil.

Üstelik SDL tarafında historically isimlendirme biraz kaygan olmuş. SDL2 için geçmişte autotools ve CMake’in farklı shared library adı/soname ürettiği bizzat SDL tartışmalarında geçmiş: biri libSDL2-2.0.so, diğeri libSDL2.so çizgisine kayabiliyordu. Yani “SDL2_image neye bakar?” sorusunun cevabı biraz da hangi upstream build sistemiyle ürettiğine bağlı.

Bu yüzden sadece:



libSDL2_image.so diye rename etmek

yeterli olmayabilir. Çünkü libSDL2_image.so içindeki NEEDED hâlâ libSDL2-2.0.so.0 diyorsa, loader gidip onu arar.



3) “En kötü RPATH ile unversioned dosyaya baktırır mıyım?”

Tek başına hayır.

RPATH ile ancak:

libSDL2-2.0.so.0 adını

senin belirttiğin klasörlerde ara

dersin. Ama adı libSDL2.so yapmak istiyorsan, NEEDED/install_name patch de gerekir.

Pratikte gerekenler:



Linux

patchelf --set-soname libSDL2.so

bağımlılarda patchelf --replace-needed libSDL2-2.0.so.0 libSDL2.so

sonra RPATH/RUNPATH ayarla

macOS

install_name_tool -id @rpath/libSDL2.dylib libSDL2.dylib

bağımlılarda install_name_tool -change oldname @rpath/libSDL2.dylib target.dylib

executable’a / dependent dylib’e LC_RPATH ekle

Yani “RPATH override” tek başına değil; RPATH + name metadata rewrite.



4) Sana önerdiğim en az bela çıkaran yaklaşım

Açık konuşayım:

Tamamen unversioned tek dosya rejimi, özellikle SDL satellite libs + transitive deps dünyasında biraz “ben kendime özel distro yazıyorum” kafası. Çalıştırırsın, ama bakım maliyeti artar.

Ben olsam 3 seçenekten birini seçerim:



Seçenek A — En sağlamı

vcpkg çıktısını olduğu gibi bırak, NuGet’e koyarken symlink’leri gerçek dosya olarak materialize et.

Artısı:

upstream ABI/name beklentileri bozulmaz

transitive deps daha az sürpriz çıkarır

Eksisi:



paket şişer

ama en az saç yolduran yol bu

Seçenek B — Linux/macOS için “runtime canonical names”e in

Symlink zincirini kaldır ama:



Linux’ta gerçek dosya adını SONAME ile aynı yap (libfoo.so.1)

bare .so’yu runtime paketine hiç koyma

build/link için gerekiyorsa ayrı staging mantığı kur

Bu, “tek dosya” hedefine yaklaşır ama yine de unversioned değil, SONAME-level versioned olur. Bence en mantıklı kompromi bu. Debian policy’nin anlattığı modele de en yakın güvenli sapma bu.



Seçenek C — Tam unversioned

libSDL2.dylib

libSDL2_image.dylib

libSDL2.so

libSDL2_image.so

Bunu ancak tüm dependency graph üzerinde:



SONAME/install_name

NEEDED/change

RPATH

test matrix

hepsini sen kontrol edeceksen öneririm. Yoksa bir gün SDL2_image değil, onun altındaki libpng, jpeg, tiff, webp, zstd falan sana “sürpriz, ben eski adı arıyordum” diye geri döner.



5) Sonuç

Sorunun düz cevabı:



Evet, overlay port ile symlinksiz çıktı aldırmak mümkün.

Sadece overlay triplet bunun için yeterli değil; o daha çok dynamic/shared build açmak için.

Evet, SDL2_image veya başka bir bağımlı lib büyük ihtimalle hâlâ versioned isim arar; bu içteki NEEDED / install_name’a bağlıdır.

Hayır, bunu yalnızca RPATH ile unversioned ada çeviremezsin.

Evet, RPATH + SONAME/install_name + replace-needed/change patch kombinasyonuyla çevirebilirsin.

Benim net tavsiyem:

“tek unversioned file” yerine “tek canonical runtime file” hedefle.

Yani:



Linux: libSDL2.so.0, libSDL2_image.so.0

macOS: @rpath/libSDL2.dylib, @rpath/libSDL2_image.dylib

Bu tasarım hem symlink ihtiyacını azaltır hem de loader’ın mantığıyla kavga etmez. Daha az cursed, daha az 3 AM production archaeology.

İstersen bir sonraki mesajda sana doğrudan:



overlay port patch stratejisi,

post-build patchelf/install_name_tool pipeline’ı,

SDL2 + SDL2_image + SDL2_ttf için örnek script

şeklinde somut bir plan çıkarayım.



Repoya bakıyorum; amacın “satellite paket kendi transitiflerini sırtlansın” gibi görünüyor. Triplet’in bunu ne kadar doğru yerden çözdüğünü ve nerede duvara toslayabileceğini net çıkaracağım.

Kodun kendisi baya net: default static, SDL family dynamic override. Güzel haber şu, “dependency bake etme” hedefin gerçekten triplet seviyesinde uygulanmış görünüyor; kötü haber, bunun symlink/versioned-name problemine tek başına dokunmadığı da açık.

Thought for 42s

Baktım. İlk verdict:

Triplet’in yaptığı iş doğru yönde ve bayağı akıllıca.

Senin esas hedefin “SDL satellite shared kalsın, ama png/jpeg/freetype/zlib gibi transitifler ayrı runtime dependency olarak akmasın, satellite içine bake olsun” ise, bu triplet tam olarak onu hedefliyor: default VCPKG_LIBRARY_LINKAGE=static, SDL family için per-port override ile dynamic. _hybrid-common.cmake bunu açıkça yapıyor.

Koddan gördüğüm omurga şu:



genel dünya: VCPKG_LIBRARY_LINKAGE static

SDL ailesi: regex ile dynamic

Linux/macOS: -fvisibility=hidden

Linux: VCPKG_FIXUP_ELF_RPATH ON

Windows: VCPKG_BUILD_TYPE release

Bu yüzden “ana satellite’a dependency bake ediyorum” cümlen teknik olarak mantıklı. README’de de zaten bunu amaç olarak dokümante etmişsin: transitifler static, SDL family dynamic, collision da böyle azalıyor.

Ama burada önemli bir ayrım var:



Bu triplet neyi çözüyor, neyi çözmüyor?

Çözdüğü şey

sdl2-image, sdl2-ttf, sdl2-mixer gibi SDL satellite’ların

zlib, libpng, freetype, vs. gibi transitiflerini

ayrı .dll/.so/.dylib olarak taşımak yerine

mümkün olduğunca static link ile içine aldırıyor.

Çözmediği şey

Bu triplet symlink/versioned filename konusunu çözmüyor.

Yani Linux/macOS’te hâlâ upstream port nasıl shared lib üretiyorsa:

libSDL2-2.0.so.0

libSDL2_image-2.0.so.0

libSDL2.dylib + install_name ilişkileri

symlink zincirleri

onlar aynı dünyada kalır. Çünkü sen burada sadece linkage preference ve birkaç build flag ayarlıyorsun; dosya adı, SONAME, install_name, install step symlink creation gibi şeylere dokunmuyorsun. vcpkg’nin triplet docs’unda da VCPKG_LIBRARY_LINKAGE sadece “preferred linkage” olarak tanımlanıyor; port desteklemiyorsa ignore bile edilebiliyor.

Yani net söyleyeyim:

Bu triplet “dependency bake” için iyi. “tek symlinksiz unversioned native file” için yeterli değil.

Güçlü tarafları

Bence iyi kararlar:



1) Per-port override yaklaşımı doğru

PORT MATCHES ... => dynamic yaklaşımı vcpkg dünyasında legit. GitHub discussion’da da triplet’lerin per-port linkage kontrolü için kullanıldığı açıkça söyleniyor.



2) -fvisibility=hidden çok yerinde

Bu özellikle Unix tarafında önemli. Senin modelinde satellite shared kalıyor ama içine statik transitifler gömülüyor. Visibility’yi kısmazsan teoride o transitiflerden sembol sızdırma riski artar. README’de bunu zaten “prevent transitive symbol leakage” diye yazmışsın; mantıklı.



3) Windows’ta release-only mantıklı

NuGet runtime assets için debug DLL taşımayacaksan VCPKG_BUILD_TYPE release bayağı temiz karar.



Zayıf tarafları / potansiyel mayınlar

Burada biraz şeytanın avukatlığını yapayım.



1) Regex listesi kırılgan

Şu an regex’in:

^(sdl2|sdl2-image|sdl2-mixer|sdl2-ttf|sdl2-gfx|sdl2-net)$

Bu çok kontrollü ama manuel bakım istiyor.

Yarın:

sdl3

sdl3-image

sdl2-image[foo] gibi feature-driven varyasyonlar

başka satellite/adaptor portlar

geldiğinde unutma riski var. README’de bunu zaten bakım kuralı olarak yazmışsın; yani riskin farkındasın. Güzel.

Ben burada tek eleştiri getiririm:

Bu strateji çalışır ama policy by regex olduğu için “bir port eklendi, test geçmedi, neden?” tarzı sürpriz çıkarır. Yani teknik borcu küçük ama gerçek.

2) VCPKG_C_FLAGS ve VCPKG_CXX_FLAGS set etmen append değil replace gibi

Şu an doğrudan:



set(VCPKG_C_FLAGS "-fvisibility=hidden")

set(VCPKG_CXX_FLAGS "-fvisibility=hidden -fvisibility-inlines-hidden")

Bu çoğu durumda fine olabilir, ama triplet veya toolchain katmanında başka flag’ler de geliyorsa bunları overwrite etme riski var. vcpkg docs, toolchain ve forwarded vars açısından dikkatli ol diyor.

Ben burada daha savunmacı olurdum:



mevcut değer varsa append et

yoksa set et

Yani mantık doğru, uygulama biraz daha defensif olabilir.



3) Linux’ta VCPKG_FIXUP_ELF_RPATH sihirli değnek değil

Bunu sen zaten Linux’a özel koymuşsun. İyi. Ama bu değişken her dependency graph derdini çözmüyor. vcpkg issue’da da görüldüğü gibi VCPKG_FIXUP_ELF_RPATH bazı dependency resolution durumlarında “tam çözüm” değil; özellikle private deps ve beklenen RUNPATH davranışları konusunda sınırları var.

Senin modelinde transitifler static olduğu için bu risk azalıyor, ama SDL core’un kendisi tek allowed external dynamic dependency ise, paketin son halinde gerçekten:



readelf -d libSDL2_image.so

otool -L libSDL2_image.dylib

çıktılarında sadece beklediğin dependency’ler kaldığını CI’da assert etmen lazım. README’de “HybridStaticValidator” dediğin kısım zaten buna işaret ediyor; o çok doğru refleks.



4) macOS tarafında bu triplet symlink/install_name problemine hiç dokunmuyor

Bunu özellikle altını çizeyim çünkü senin son soruların bunun etrafında dönüyordu.

x64-osx-hybrid.cmake sadece:



arch

Darwin

VCPKG_OSX_ARCHITECTURES

common include

yapıyor. _hybrid-common da sadece linkage/flags seviyesinde. Yani:



install_name

@rpath

versioned vs unversioned dylib naming

symlink creation

burada kontrol edilmiyor.

Dolayısıyla “tripletim var, dependency bake ettim” evet.

Ama “macOS dylib topology’yi ben yönettim” henüz değil.

En önemli sonuç

Senin mevcut custom triplet mimarin şu iş için başarılı:



“SDL satellites shared olsun ama transitif runtime DLL/SO/DYLIB sayısı minimum olsun.”

Ama şu iş için yetersiz:



“Linux/macOS’te symlinksiz, tek canonical/unversioned shared file rejimi kurayım.”

O ikinci hedef için sana ayrıca overlay port veya post-build rewrite gerekir.



Ben olsam ne yapardım?

Üç katmanlı düşünürdüm:



Katman 1 — Bu triplet’i koru

Bence çöpe atılacak bir şey değil. Tam tersine, mimarinin çekirdeği bu olabilir. Özellikle Windows/Linux/macOS arasında ortak packaging policy vermesi iyi.



Katman 2 — “naming/install metadata”yı port overlay’e taşı

Özellikle şu portlar için:



sdl2

sdl2-image

sdl2-ttf

belki sdl2-mixer

Orada patch’lersin:



Linux: SONAME, gerçek dosya adı, symlink creation

macOS: install_name, @rpath, gerekirse install_name_tool

Yani linkage policy = triplet

binary identity / loader metadata = overlay port

Bence doğru separation of concerns bu.



Katman 3 — CI’da binary audit koy

Her RID için:



Linux: readelf -d, objdump -p, ldd kontrollü

macOS: otool -L

Windows: dumpbin /dependents veya uygun tool

Ve assert:



satellite yalnızca izinli dependency’leri taşıyor mu?

versioned name mi çıktı?

symlink oluştu mu?

unwanted transitive dynamic dep kaldı mı?

Bu kısım olmazsa bir gün upstream port değişir, triplet “aynı” görünür ama binary layout sessizce kayar.



Kısacası

Benim dürüst review’üm:

Artıları



yaklaşım doğru

vcpkg’nin desteklediği bir pattern

hedefinle uyumlu

transitif dependency explosion problemini iyi azaltıyor

Eksileri



symlink/versioned-name problemini çözmüyor

regex/policy kırılgan

flags kısmı biraz daha defensif yazılabilir

RPATH’e fazla güvenmemek lazım

Ve en net cümle:

Sen “dependency bake etme” kısmını becermişsin, evet.

Ama “loader-level binary naming topology” kısmı hâlâ ayrı problem.

İstersen bir sonraki mesajda sana çok somut şekilde şunu çıkarayım:



bu mevcut hybrid triplet’e dokunmadan

sdl2 / sdl2-image overlay port’larında

Linux/macOS için

symlink azaltma + SONAME/install_name rewrite

nasıl tasarlanır, dosya dosya.

şurada bir takım overlay port'larım da var ama farklı amaçlar için https://github.com/janset2d/sdl2-cs-bindings/tree/master/vcpkg-overlay-ports Sence nasıl bir overlay portu yazmak lazım amacıma ulaşmak için

Şimdi overlay port tarafını da okuyup sana “hangi katmanda ne patch’lenmeli” diye net bir tasarım çıkarıyorum. Burada kritik fark şu olacak: triplet policy veriyor, port ise binary’nin kimliğini gerçekten değiştiriyor.

Yön netleşti: senin burada istediğin şey “linkage policy” değil, “binary kimliği ve loader beklentisi”. O yüzden yeni overlay port’ların işi CMake bayrağı açıp kapatmak değil; install sonrası çıkan binary’yi normalize etmek olmalı.

Thought for 33s

Bence amacına ulaşmak için yazman gereken overlay port tipi şu:

“policy overlay” değil, “binary normalization overlay”.

Senin mevcut overlay port klasörünün amacı zaten upstream bug’ları ve packaging mismatch’lerini lokal override ile düzeltmek; README bunu açıkça böyle tanımlıyor. Şu an aktif overlay’lerin de karakteri aynı: sdl2-mixer codec/policy değiştiriyor, sdl2-gfx ise Unix visibility problemini port-local patch ile çözüyor.

Senin hybrid triplet’in ise başka bir katmanda çalışıyor: default linkage’ı static yapıyor, SDL ailesini tekrar dynamice çekiyor, Unix’te -fvisibility=hidden, Linux’ta VCPKG_FIXUP_ELF_RPATH açıyor. Yani triplet şu an “hangi şey static/dynamic olsun” kararını veriyor; “çıkan .so/.dylib dosyasının adı, SONAME’i, install_name’i ne olsun” kısmını yönetmiyor.

Bu yüzden benim önerim:



Ne tür overlay yazmalısın?

Her SDL dynamic portu için küçük ama net amaçlı overlay:



sdl2

sdl2-image

sdl2-ttf

gerekirse sdl2-mixer

belki sonra sdl2-net

Amaçları:



upstream build’i bozmadan paket çıksın

install sonrası binary adını normalize et

Linux’ta SONAME / NEEDED zincirini düzelt

macOS’te install_name / dependent library path’leri @rpath/... olacak şekilde düzelt

symlink yaratılmasını ya engelle ya da install sonunda sil

CI’da audit et

Yani port overlay’in kalbi şu olacak:



upstream portfile’ı büyük ölçüde aynen koru

en alta “Janset normalization step” ekle

gerekiyorsa ufak patch + post-build script kullan

Bu model, vcpkg overlay port mantığına da uygun: overlay port zaten belirli port tarifini override etmek için var.

Hedef dosya rejimi ne olmalı?

Burada sana biraz karşı çıkayım:

tam unversioned Linux .so hedefi bence gereksiz riskli.

Daha sağlıklı hedef:



Linux

Tek gerçek runtime file:



libSDL2.so.0

libSDL2_image.so.0

libSDL2_ttf.so.0

Ve mümkünse:



symlink yok

ELF SONAME = gerçek dosya adı

dependent libs’in DT_NEEDED kayıtları da bu canonical isme bakıyor

macOS

Tek gerçek runtime file:



libSDL2.dylib

libSDL2_image.dylib

libSDL2_ttf.dylib

Ve:



install_name = @rpath/libSDL2.dylib

dependents de @rpath/libSDL2.dylib benzeri isme bakıyor

Sebep şu: ELF tarafında RPATH sadece nerede aranacağını belirler; hangi library adının aranacağını DT_NEEDED / SONAME belirler. patchelf bunu sonradan değiştirebilir: --replace-needed, --set-soname, --set-rpath destekliyor. macOS tarafında da aynı rolü install_name_tool görüyor; run-path dependent library modeli @rpath/... ile çalışıyor.

Yani “RPATH ile unversioned’e baktırayım” tek başına yetmez.

Gerçek çözüm:

Linux: SONAME + NEEDED + RPATH

macOS: install_name + dependent path rewrites + LC_RPATH

Nasıl bir overlay port tasarımı?

Ben bunu iki pattern’e bölerdim.



Pattern A — Source-level normalization varsa

Upstream build sistemi zaten uygun knob veriyorsa:



OUTPUT_NAME

VERSION

SOVERSION

Apple install name ayarları

bunları patch’lersin.

Bu en temiz yol, ama her SDL portunda aynı kolaylıkta olmayabilir.



Pattern B — Post-install binary rewrite

Bence senin durumda daha pratik ve daha az kırılgan başlangıç bu.

Portfile’ın sonunda:



upstream portu normal build et

installed tree’de target library’yi bul

canonical ada rename et

Linux’ta:patchelf --set-soname ...

dependent libs için patchelf --replace-needed old new ...

patchelf --set-rpath '$ORIGIN' ya da senin paket layout’una uygun yol

macOS’te:install_name_tool -id @rpath/libSDL2_image.dylib ...

dependents için install_name_tool -change old @rpath/libSDL2.dylib ...

symlink’leri kaldır

audit step koş

Bu modelin güzelliği şu:



upstream source patch’i minimum olur

baseline bump’te daha az merge sıkıntısı yaşarsın

logic senin elinde kalır

Ben olsam hangi overlay’leri yazarım?

1) sdl2 overlay

Bu en kritik port.

Görevleri:



Linux canonical name’i belirlemek (libSDL2.so.0 veya upstream’in major naming’ine uyumlu eşdeğer)

SONAME’i bu ada sabitlemek

macOS’te install_name’i @rpath/libSDL2.dylib yapmak

install symlink cleanup

Neden önce bu?

Çünkü tüm satellite’lar dönüp buna bakacak. Core’u normalize etmeden satellite normalizasyonu yarım kalır.

2) sdl2-image overlay

Görevleri:



kendi canonical filename/SONAME/install_name’ını düzenlemek

core SDL dependency adını senin seçtiğin canonical isme rewrite etmek

transitiflerde unwanted dynamic dep kalmadığını assert etmek

Yani audit:



Linux readelf -d

macOS otool -L

çıktısında sadece beklenen dependency’ler olsun.



3) sdl2-ttf overlay

Aynı model.



4) sdl2-mixer overlay

Bunda senin zaten başka amaçlı overlay’in var. Ben burada ayrı overlay yerine mevcut sdl2-mixer overlay’i genişletirdim. Zaten o portta codec policy için yerel override kullanıyorsun; naming normalization’ı da oraya eklemek mantıklı. README’de bu overlay’in upstream design mismatch için var olduğu açık.

Portfile içinde mantık nasıl ayrılmalı?

Ben olsam helper script mantığı kurarım.

Mesela overlay port altında:



portfile.cmake

normalize-elf.cmake

normalize-mach-o.cmake

belki audit-elf.cmake

belki audit-mach-o.cmake

portfile.cmake içinde:



upstream logic

patch list

sonra platforma göre normalize helper çağrısı

Bunun faydası:



aynı şeyi sdl2, sdl2-image, sdl2-ttf için copy-paste manyaklığına çevirmemiş olursun

ama vcpkg overlay port mantığını da bozmazsın

Tam ortak helper’ı overlay klasörü altında paylaşmak bence doğru. Şu anki overlay’lerinde de “tam byte-identical upstream + küçük lokal patch” yaklaşımın var; bu disiplin burada da işe yarar.

Benim önerdiğim karar ağacı

Linux için

Hedef:



symlinksiz tek gerçek file

ama unversioned değil, SONAME-level canonical

Örnek:



file: libSDL2_image.so.0

SONAME: libSDL2_image.so.0

Sonra tüm dependents:



DT_NEEDED libSDL2.so.0

Bu, loader mantığıyla daha barışık.



macOS için

Hedef:



symlinksiz tek gerçek file

file name unversioned olabilir

install_name hep @rpath/<name>.dylib

Örnek:



file: libSDL2_image.dylib

id: @rpath/libSDL2_image.dylib

dependency: @rpath/libSDL2.dylib

Bu da Apple’ın run-path dependent library modeline tam oturur.

Neyi yapmanı önermem?

1) Sadece RPATH ile çözmeye çalışma

Bu, loader’a “mahalle bu” der; “aradığın kişi Ahmet değil Mehmet” demez.



2) Sadece triplet üzerinden çözmeye çalışma

Triplet build policy yeridir; binary surgery yeri değil. vcpkg triplet değişkenleri bu amaç için fazla dolaylı kalıyor.



3) Tüm normalization’ı tek generic “after harvest” script’e gömme

Bu kısa vadede cazip ama uzun vadede neden hangi portta neyin değiştiği kaybolur.

SDL2_gfx overlay’inde yaptığın gibi port-local gerekçeyi korumak daha iyi tasarım. README’de de zaten her overlay için “why / based on / what changed” disiplini var; bunu bozma.

Benim net önerim

Ben olsam şu ADR-benzeri kararı alırım:

Decision:

Hybrid triplet aynen kalır.

Binary naming / loader metadata normalization için sdl2, sdl2-image, sdl2-ttf, sdl2-mixer overlay port’ları eklenir.

Linux rule:

Unversioned .so hedeflenmez.

Her dynamic SDL binary tek bir canonical runtime filename ile paketlenir; filename = SONAME.

macOS rule:

Tek real .dylib dosyası paketlenir; install_name = @rpath/<canonical>.dylib.

Implementation rule:

Mümkün olduğunca post-install rewrite. Source patch yalnız gerekiyorsa.

Validation rule:

CI’da harvested binary üzerinde dependency/audit assertion zorunlu.

Bence bu yaklaşım hem senin mevcut mimarine oturur, hem de “ben packaging topology’yi kontrol ediyorum” derken loader’la bilek güreşi yapmanı minimuma indirir.

İstersen bir sonraki mesajda sana doğrudan şu dört şeyi çıkarayım:



sdl2 overlay portfile skeleton’ı

Linux için patchelf adım listesi

macOS için install_name_tool adım listesi

CI’da koşulacak audit check’lerin net checklist’i

ama .NET c#binding ve native wrapper dünyasında nuget paketlerinin RID'lerine genelde unversioned lar konuluyor, standard gibi birşey bu

Haklısın, pratikte NuGet runtime asset dünyasında kullanıcıların gördüğü dosya adı çoğu zaman unversioned oluyor. Ama burada ince çizgi şu: “paketin içine unversioned koymak” ile “native toolchain’in/reloader’ın iç metadata’sını da unversioned yapmak” aynı şey değil; onu netleştireceğim.

Thought for 31s

Evet, ekosistem pratiği olarak haklısın. NuGet’in resmi “native files in .NET packages” örneği de RID klasörlerinde unversioned dosya adları kullanıyor: runtimes/linux-x64/native/libcontoso.so, runtimes/osx-x64/native/libcontoso.dylib, runtimes/win-x64/native/contoso.dll. Yani .NET/NuGet tarafında görünen yüz gerçekten çoğu zaman bu.

Ama ben burada şunu ayırmaya çalışıyordum:

“NuGet paketinde unversioned dosya taşımak” başka şey,

“native binary’nin kendi loader metadata’sını da unversioned yapmak” başka şey.

.NET’in native library loading davranışı da bunu destekliyor. Mesela [DllImport("nativedep")] dersen .NET Linux/macOS’ta nativedep.so / libnativedep.so ya da nativedep.dylib / libnativedep.dylib varyasyonlarını dener. Ama [DllImport("nativedep.so.6")] gibi versioned bir ad verirsen bu kez versioned ada göre arama yapar. Yani managed tarafta unversioned isim kullanmak, paket tarafında unversioned file taşıma geleneğini bayağı doğal kılıyor.

O yüzden senin bağlamında daha doğru cümle şu olur:

NuGet RID asset dünyasında unversioned filename neredeyse fiili standart.

Ama bunun çalışması için altta iki modelden biri gerekir:

Paket içine gerçekten libfoo.so / libfoo.dylib koyarsın.

Ya da versioned gerçek dosya bırakırsın, ama symlink veya custom resolver ile managed import’un aradığı unversioned adı çözersin.

Benim önceki itirazım “unversioned koyma”ya değildi aslında.

İtirazım şuydu:

Sadece RPATH ile bunu çözemezsin.

Çünkü .NET önce hangi isimle load edeceğine karar veriyor, sonra probing path / loader devreye giriyor.

Eğer managed taraf SDL2_image ya da libSDL2_image.so gibi bir şey arıyorsa, altta sadece libSDL2_image.so.0 varsa bu otomatik olarak her zaman sihirli şekilde eşleşmez.

Ben bu yüzden “binary normalization overlay” dedim.

Senin hedefin NuGet-ekosistem açısından bence gayet meşru:



runtimes/linux-x64/native/libSDL2.so

runtimes/linux-x64/native/libSDL2_image.so

runtimes/osx-x64/native/libSDL2.dylib

runtimes/osx-x64/native/libSDL2_image.dylib

Bu paket yüzeyi .NET dünyasına daha doğal oturur. Hatta Microsoft’un paketleme örneği de tam bu şekle benziyor.

Benim asıl çekincem şu teknik detaydaydı:



libSDL2_image.so dosyasının kendi içinde DT_NEEDED kaydı hâlâ upstream versioned ada bakıyorsa,

ya da macOS’te install name eski/versioned biçimdeyse,

bu kez dışarıdan dosya adı temiz görünse bile içeride dependency zinciri hâlâ upstream beklentisiyle yaşayabilir.

Bu yüzden gerektiğinde patchelf --replace-needed / --set-soname ve macOS’te install_name_tool -id/-change tarafı devreye girer. patchelf bu işlevleri açıkça destekliyor.

Benim güncellenmiş pozisyonum şu:



Evet, NuGet RID native asset tarafında unversioned yüzey bence doğru hedef.

Hayır, Linux tarafında upstream’in versioned/symlinkli çıktısını aynen korumak zorunda değilsin.

Ama, bunu sadece “dosyayı rename edeyim” seviyesinde bırakmak riskli.

Yapman gereken şey: NuGet-facing filename unversioned, gerekirse binary internal references normalize edilmiş.

Yani pratik tasarım bence şöyle olmalı:



Paketin dış yüzeyi: unversioned

Internal binary metadata: senin seçtiğin packaging modeline uyumlu olacak şekilde patch’lenmiş

Managed imports: unversioned

CI audit: readelf/otool ile kontrol

Kısacası:

Senin sezgin doğru, benim caution’ım da boşa değil.

Ekosistem “unversioned package surface” seviyor; native loader ise “ad + metadata” konusunda nazlı bir arkadaş. İkisini evlendiren overlay port lazım, olay bu.

İstersen bir sonraki cevapta sana doğrudan şu kararı netleştireyim:

SDL2 family için en doğru model



real file unversioned + patched metadata mı,

yoksa

real file versioned + unversioned duplicate/symlink-resolved facade mı?

Bence ikinciden kaçınmak istiyorsun; ben de sana birincinin nasıl uygulanacağını somutlayayım.

Deniz, NuGet ekosistemi konusundaki noktan kesinlikle doğru ve tartışmanın kilidini açan argüman da bu zaten. Sadece DllImport mekanizmasının varsayılan davranışından (unversioned aramasından) dolayı değil, aynı zamanda .nupkg formatının doğasından dolayı da haklısın. NuGet temelde bir ZIP arşividir ve ZIP formatı içinde symlink taşımak/korumak cross-platform dünyasında her zaman mayın tarlası olmuştur.

O yüzden fikrimi çok net belirteyim: Senin senaryonda "Real file unversioned + patched metadata" (Gerçek dosyanın unversioned olması + iç metadatasının buna göre yamalanması) açık ara en doğru ve tek pratik yoldur. Symlink'leri bir şekilde NuGet içine tıkıştırmaya çalışmak veya managed tarafta custom bir DllImportResolver yazıp versioned isimleri map etmek gereksiz bir "workaround" olurdu.

Bu stratejiyi vcpkg overlay portları ile nasıl hayata geçireceğinin net haritasını çıkaralım:

Temel Strateji: Post-Build Binary Surgery
Overlay portlarında upstream kaynak kodunu (CMakeLists.txt vs.) patch'leyerek unversioned çıktı almaya çalışmak, her SDL güncellemesinde başını ağrıtır. Bunun yerine upstream'in kendi bildiği gibi (versioned ve symlink'li) derlemesine izin verip, vcpkg'nin install aşamasının hemen sonrasında çıkan binary'leri ameliyat etmek (post-build surgery) en stabil yöntemdir.

Overlay portlarındaki portfile.cmake dosyanın en sonuna ekleyeceğin adımlar platform bazında şu şekilde olmalı:

1. Linux Adımları (ELF Ameliyatı)
Linux'ta patchelf senin en iyi dostun olacak. Amacımız symlink'leri silip, ana dosyayı unversioned isme çevirmek ve içindeki bağımlılık ağacını bu yeni isme yönlendirmek.

sdl2 portu için:

Dosyayı yeniden adlandır: mv libSDL2-2.0.so.0.X.Y libSDL2.so

Symlink'leri temizle: rm libSDL2-2.0.so* libSDL2.so.0*

Kendi SONAME'ini unversioned yap: patchelf --set-soname libSDL2.so libSDL2.so

sdl2-image (ve diğer satellite) portları için:

Dosyayı yeniden adlandır: mv libSDL2_image-2.0.so.0.X.Y libSDL2_image.so

Symlinkleri sil.

Kendi SONAME'ini düzelt: patchelf --set-soname libSDL2_image.so libSDL2_image.so

Kritik Adım - Bağımlılığı Güncelle: patchelf --replace-needed libSDL2-2.0.so.0 libSDL2.so libSDL2_image.so

RPATH'i ayarla (NuGet klasöründe yan yana duracakları için): patchelf --set-rpath '$ORIGIN' libSDL2_image.so

2. macOS Adımları (Mach-O Ameliyatı)
Apple tarafında işler install_name_tool ile yürüyecek. Burada her şey @rpath etrafında dönmeli.

sdl2 portu için:

Dosyayı yeniden adlandır: mv libSDL2-2.0.0.dylib libSDL2.dylib

Symlink'leri sil.

Kendi kimliğini (id) güncelle: install_name_tool -id @rpath/libSDL2.dylib libSDL2.dylib

sdl2-image (ve diğer satellite) portları için:

Dosyayı yeniden adlandır: mv libSDL2_image-2.0.0.dylib libSDL2_image.dylib

Symlink'leri sil.

Kendi kimliğini güncelle: install_name_tool -id @rpath/libSDL2_image.dylib libSDL2_image.dylib

Kritik Adım - Bağımlılığı Güncelle: install_name_tool -change <eski_sdl2_install_name> @rpath/libSDL2.dylib libSDL2_image.dylib (Buradaki eski ismi otool -L ile dinamik olarak yakalayabilirsin).

LC_RPATH ekle (eğer yoksa): install_name_tool -add_rpath @loader_path libSDL2_image.dylib

Neden Bu Yaklaşım En İyisi?
.NET Beklentisiyle %100 Uyum: Managed kod hiçbir efor sarf etmeden [DllImport("SDL2")] ile çalışır.

NuGet Dostu: Ortada symlink kalmadığı için paketleme sırasında boyut şişmesi veya arşiv bozulması yaşamazsın.

Upstream İzolasyonu: SDL'in kendi karmaşık build sistemini (CMake/Autotools) "bana symlinksiz dosya ver" diye zorlamazsın. Upstream ne yaparsa yapsın, sen çıkan dosyayı kendi standartlarına göre formatlarsın.

Bu post-build surgery adımlarını her SDL satellite portu için ayrı ayrı yazmak yerine, vcpkg overlay dizininde ortak bir CMake helper fonksiyonu (örneğin vcpkg_normalize_dotnet_native_libs) yazıp tüm portfile.cmake'lerden tek satırla çağırmak sence de uzun vadeli bakım maliyetini minimuma indirmez mi?

Ok ama vcpkg native çözüm var mı?

Kısa ve net cevap: Hayır, vcpkg'nin içinde set(VCPKG_DISABLE_SYMLINKS ON) veya VCPKG_FLATTEN_SHARED_LIBS gibi sihirli, yerleşik (native) bir konfigürasyon bayrağı yok.

Bunun tasarımsal bir sebebi var: vcpkg, ABI (Application Binary Interface) isimlendirmesi ve versiyonlama işine bilerek karışmaz. Felsefesi, arka plandaki derleme sistemi (CMake, Autotools, vb.) ne üretiyorsa onu paketlemektir. Upstream port (örneğin SDL2) CMakeLists.txt içinde set_target_properties(... PROPERTIES SOVERSION 2.0) diyorsa, vcpkg buna saygı duyar ve o symlink zincirini oluşturur.

Eğer "vcpkg native çözüm"den kastın, vcpkg ekosisteminin kendi kurallarına ve ruhuna (idiomatic vcpkg) uygun hareket etmekse, önünde iki yol var. İlki kağıt üzerinde "en doğru" ama pratikte en acı verici olanı, ikincisi ise benim savunduğum surgery yönteminin vcpkg kılıfına uydurulmuş hali:

1. "Gerçek" vcpkg Yolu: Source Patching (Önermem)
vcpkg'nin bir şeyin build çıktısını değiştirmek için sunduğu resmi mekanizma .patch dosyalarıdır. Overlay portunun içine bir remove-versioning.patch koyarsın ve portfile.cmake içinde bunu uygularsın.

Bu patch gidip SDL2'nin ve satellite'larının CMakeLists.txt dosyalarındaki şu satırları bulup siler veya değiştirir:

VERSION ve SOVERSION property'leri.

macOS için MACHO_COMPATIBILITY_VERSION vb. ayarları.

Neden önermiyorum? Çünkü SDL ailesinin CMake scriptleri devasa ve çok dallı budaklıdır. Yarın SDL 2.30'dan 2.32'ye geçtiğinde o patch'in satır numaraları kayacak, chunk'lar apply edilemeyecek ve CI pipeline'ın kırmızıya dönecektir. Bakım maliyeti (technical debt) çok yüksektir.

2. İdiomatik vcpkg Yolu: vcpkg_execute_process ile Custom Helper
Madem yerleşik bir bayrak yok, vcpkg'nin native araçlarını kullanarak benim bahsettiğim "post-build surgery" işlemini vcpkg'nin bir parçası haline getirirsin. vcpkg'de post-build custom operasyonlar vcpkg_execute_process veya vcpkg_execute_required_process ile yapılır.

Yani dışarıda bir bash script çalıştırmak yerine, portfile.cmake dosyanın en sonuna, tam file(INSTALL ...) adımından sonra şu tarz bir vcpkg native bloğu eklersin:

CMake
# portfile.cmake'in sonu (Kurulum bittikten sonra)

if(VCPKG_TARGET_IS_LINUX AND VCPKG_LIBRARY_LINKAGE STREQUAL "dynamic")
    # vcpkg'nin kendi patchelf kopyasını veya sistemdekini bulalım
    find_program(PATCHELF NAMES patchelf REQUIRED)

    message(STATUS "Normalizing ELF soname and filenames for .NET RID layout...")

    # Symlink'leri bul ve sil (vcpkg'nin yerleşik dosya manipülasyonu)
    file(GLOB SYMLINKS "${CURRENT_PACKAGES_DIR}/lib/libSDL2-2.0.so.*")
    if(SYMLINKS)
        file(REMOVE ${SYMLINKS})
    endif()

    # Gerçek dosyayı bul ve rename et
    file(RENAME "${CURRENT_PACKAGES_DIR}/lib/libSDL2-2.0.so.0.x.y" "${CURRENT_PACKAGES_DIR}/lib/libSDL2.so")

    # patchelf'i vcpkg native wrapper'ı ile çağır
    vcpkg_execute_required_process(
        COMMAND ${PATCHELF} --set-soname libSDL2.so "${CURRENT_PACKAGES_DIR}/lib/libSDL2.so"
        WORKING_DIRECTORY "${CURRENT_PACKAGES_DIR}/lib"
        LOGNAME "patchelf-set-soname-${TARGET_TRIPLET}"
    )
endif()
Özetle
Bir flag açıp arkanı yaslayabileceğin bir "native" çözüm yok. Karşına çıkan symlink ve SOVERSION durumu, ELF/Mach-O formatlarının ve POSIX dünyasının bir gerçeği. vcpkg bunu soyutlamak yerine olduğu gibi yansıtıyor.

Eğer .NET'in unversioned beklentisiyle POSIX'in versioned gerçekliğini barıştırmak istiyorsan, o köprüyü overlay portlarında yukarıdaki gibi vcpkg_execute_required_process kullanarak kendin inşa etmelisin. Bence pragmatik olan, kırılgan source patch'lerle uğraşmak yerine, derlenmiş tertemiz çıktıyı en son adımda kendi istediğin formata sokmaktır. Ne dersin, bu tarz bir CMake helper mantığı senin mimariye uyar mı?

LLM;

Haklısın Deniz. "Patchelf komutunu kendim yazacaksam vcpkg kullanmanın esprisi ne, gider kendi bash scriptimi yazarım" demekte %100 haklısın. Olayın root cause'una, yani doğrudan "compile shape"e (derleme şekline) inmek istiyorsun.

Buna müdahale etmenin yolu var mı? Evet, kesinlikle var. Ama burada anlamamız gereken şey şu: Bu müdahale vcpkg'nin bir özelliği üzerinden değil, doğrudan CMake'in (veya upstream build sisteminin) manipüle edilmesiyle yapılıyor.

İşte aradığın o "compile shape" müdahalesinin anatomisi:

Compile Shape Nasıl Belirleniyor?
CMake dünyasında bir kütüphanenin Linux/macOS'te symlink'li (versioned) veya symlink'siz (unversioned) çıkmasını belirleyen şey SOVERSION ve VERSION (Apple için MACHO_COMPATIBILITY_VERSION vb.) özellikleridir.

Eğer SDL2'nin kaynak kodundaki CMakeLists.txt dosyasına bakarsan, adamların şu tarz satırları hardcoded olarak yazdığını görürsün:

CMake
set_target_properties(SDL2 PROPERTIES 
    VERSION ${SDL_VERSION}
    SOVERSION ${SDL_SOVERSION}
)
İşte o can sıkıcı symlink zincirini ve internal metadata'yı (SONAME) yaratan şey tam olarak bu iki satırdır.

Compile Shape'i Overlay Port ile Nasıl Ezersin? (Source Patching Yöntemi)
Upstream (SDL ekibi) CMake scriptlerinde "bana unversioned ver" diyebileceğin bir opsiyon (örneğin -DSDL_DISABLE_VERSIONING=ON gibi bir bayrak) sunmuyor.

Bu yüzden vcpkg overlay portunda aradığın native çözüm Source Patch (kaynak kodu yaması) kullanmaktır. Adım adım şöyle yaparsın:

1. Bir Patch Dosyası Oluştur (Örn: remove-versioning.patch)
SDL2'nin CMakeLists.txt dosyasındaki o VERSION ve SOVERSION property'lerini silen veya ezen bir git patch'i hazırlarsın.
Hatta daha temizi, patch ile CMakeLists.txt'nin en sonuna şu satırı eklersin:

CMake
set_target_properties(SDL2 PROPERTIES VERSION "" SOVERSION "")
2. Overlay Port'unda Bu Patch'i Uygula
Yazdığın overlay klasöründeki portfile.cmake dosyasında, kaynak kod indirilirken bu yamanın uygulanmasını söylersin:

CMake
vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO libsdl-org/SDL
    REF release-${VERSION}
    SHA512 ...
    HEAD_REF main
    PATCHES
        "remove-versioning.patch" # <-- Senin compile shape müdahalen
)
Bu kadar. vcpkg kodu indirir, senin patch'ini uygular ve CMake'i çalıştırır. Derleyici o versiyon satırlarını hiç görmediği için, anadan doğma symlink'siz, tertemiz, unversioned libSDL2.so ve libSDL2.dylib üretir. İç metadata'sı (SONAME, install_name) da zaten baştan unversioned olarak derlenmiş olur.

Neden Bunu İlk Başta Önermedim? (Trade-off)
Bunu sana ilk mesajda önermememin tek sebebi bakım maliyetidir (maintenance). Mühendislik riski şapkanla baktığında şu kırılganlığı göreceksin: Yarın vcpkg SDL 2.30'dan 2.32'ye geçtiğinde, SDL'in CMakeLists.txt yapısı ufak bir değişime uğrarsa, senin yazdığın remove-versioning.patch dosyası "hunk failed" hatası verip CI'ını patlatacak. Her majör (bazen minör) SDL güncellemesinde o patch dosyasını manuel olarak yeniden alman gerekecek.

Önceki mesajda savunduğum patchelf (post-build surgery) yöntemi ise kodu değil "çıktıyı" manipüle ettiği için upstream kodundan agnostiktir, versiyon güncellemelerinde kolay kolay kırılmaz.

Karar Senin
Eğer "Abi ben yamalı çıktı (post-build patchelf) sevmiyorum, baştan temiz compile edilsin, ben o patch'in bakımını üstlenirim" diyorsan; aradığın yöntem overlay port içine bir .patch dosyası koyup CMake'teki SOVERSION hedeflerini yok etmektir. En "kökünden" çözüm budur.

Ben;

tamam bakim maliyetini anladım ama manual PATCHELF den daha reliable olursa kabul edilebilir

LLM|

Mühendislik şapkanla bakınca bu argümanına sonuna kadar katılırım Deniz.

"Çalışma zamanı (runtime) güvenilirliği" açısından bakarsak; baştan, derleyicinin (compiler/linker) kendi eliyle ürettiği, içinde sonradan oynanmamış, string tabloları kaydırılmamış temiz bir unversioned binary elde etmek, patchelf veya install_name_tool ile "binary surgery" yapmaktan her zaman daha güvenilirdir. patchelf ne kadar standart bir araç olsa da, sonuçta derlenmiş bir dosyanın ELF header'larını deşip manipüle eden bir hack'tir.

Eğer bu "temiz compile" yönteminin tek handikabı olan "bakım maliyetini (maintenance)" kabul edilebilir bir seviyeye çekersek, senin için kusursuz çözüme ulaşmış oluruz.

Sana kırılgan ve sürekli patlayan statik .patch dosyaları yerine, vcpkg içinde endüstri standardı olarak kullanılan "Dinamik Yamama" (Dynamic Patching) yöntemini önereceğim. Bu yöntem manuel patchelf'ten daha temiz, statik patch dosyalarından ise çok daha dayanıklıdır.

Reliable Source Patching: Dinamik Müdahale
Sabit satır numaralarına bağımlı bir git patch'i (.patch) kullanmak yerine, CMake yapılandırmasından (configure) saniyeler önce portfile.cmake içinde CMakeLists.txt'ye dinamik olarak müdahale edeceğiz.

Bunun için en sağlam iki taktik şudur:

Taktik 1: Append Override (En Sağlamı)
Çoğu projede CMakeLists.txt dosyasının en sonuna kendi komutunu ekleyerek (append), upstream'in yaptığı ayarları ezebilirsin. Upstream SOVERSION set eder, sen dosyanın en sonunda onu geri alırsın.

portfile.cmake içine şu bloğu eklersin:

CMake
# Kaynak kod indirildikten SONRA, vcpkg_cmake_configure'dan ÖNCE:

# SDL2 hedefinin (target) son özelliklerini ezip unversioned yapıyoruz
file(APPEND "${SOURCE_PATH}/CMakeLists.txt" "
# --- JANSET OVERLAY: DISABLE VERSIONING ---
if(TARGET SDL2)
    set_target_properties(SDL2 PROPERTIES VERSION \"\" SOVERSION \"\")
endif()
")
Bu yöntem satır numarası kaymalarından veya upstream'in CMake'i nasıl refactor ettiğinden etkilenmez. Dosyanın sonuna eklendiği için hedef (target) oluştuktan sonra çalışır ve "symlink ve version üretme" emrini iptal eder.

Taktik 2: Regex ile Hedefli İmha
Eğer upstream kodu hedefleri alt dizinlerde (subdirectories) tanımlıyorsa ve root dosyasına append yapmak yetmiyorsa, vcpkg'nin native string replace aracını kullanırsın.

CMake
# SOVERSION veya VERSION geçen satırları bul ve değerlerini boşalt
vcpkg_replace_string("${SOURCE_PATH}/CMakeLists.txt" 
    "PROPERTIES VERSION \${SDL_VERSION} SOVERSION \${SDL_SOVERSION}" 
    "PROPERTIES VERSION \"\" SOVERSION \"\""
)
(SDL2-image, ttf gibi satellite projelerde değişken isimleri farklı olabilir, o yüzden Regex ile replace yapmak daha da sağlam bir yoldur).

Bu Yaklaşım Neleri Çözer?
Tertemiz Native Çıktı: Linux'ta libSDL2.so, macOS'te libSDL2.dylib çıkar. İçlerindeki SONAME ve install_name değerleri de CMake tarafından baştan unversioned olarak doğru yazılır.

Sıfır Symlink: CMake'in "önce ana dosyayı yarat, sonra 2 tane symlink oluştur" rutini hiç tetiklenmez. Kurulum (install) adımı paket klasörüne sadece tek bir .so / .dylib kopyalar.

Bağımlılıklar Otomatik Çözülür: SDL2_image'i derlerken o da senin overlay üzerinden unversioned bir SDL2 bulacağı için, kendi içindeki DT_NEEDED kaydını doğrudan libSDL2.so olarak yazar. Bağımlılık zinciri baştan doğru kurulur.

Maintenance Maliyeti Düşük: Statik git patch'i gibi her versiyonda kırılmaz. Append yöntemi neredeyse ölümsüzdür.

Son Karar
Madem "manual patchelf'ten daha reliable bir çözüm istiyorsun", rotan kesinlikle bu olmalı. Native CMake davranışını (Compile Shape'i), vcpkg'nin dinamik file(APPEND ...) veya vcpkg_replace_string araçlarıyla build öncesi ezmek. Bu sayede hem .NET'in istediği formata (unversioned dosya) tertemiz bir compiler çıktısıyla ulaşırsın, hem de maintenance zehirlenmesinden kurtulursun. Bu tasarım aklına yattı mı?