# Fauna — testovací plán ve hře

Sepsáno 2026-09-27 po dokončení fází 0–4 a kroku 1b z `fauna-plan.md`. Jeleny ve hře ještě nikdo neviděl, všechno níže bylo odladěné jen testy. Body jsou seřazené od základu k ladění; u každého je, co čekat a co je známé omezení, aby se nehlásilo jako bug.

## 1. Start a nalezení stáda

- Stáda jsou dvě, nejméně 60 m od tábora, na místě s nejvíc trávy. Zvířata neodkrývají mlhu, takže je třeba jít je hledat s člověkem. Sledovat, jestli se jelen za hranicí prozkoumaného ukazuje ztlumeně jako zdroj (má), nebo vůbec.
- Jelen je hnědý čtverec 1,5 m vysoký (`deer.tres`). Placeholder, ne bug; sprite čeká na sprite pipeline.
- Log má každý tick `Animals: N` vedle řádku populace. Na startu 12–20, v čase se hýbe.

## 2. Stádo jako stádo

- Drží se pohromadě kolem kotvy (`homeRadius` 15 m), kotva se za sezónu posune o 20 m seedovaným směrem. Pozor na rozběhnutí do všech stran nebo naopak slepení na jeden bod.
- Pastva: hladoví jeleni se rozejdou k **různým** trsům, ne všichni na jeden (to byl bug, opravený měřením). Nasycený se toulá, nepase.
- Kolize: poloměr 0,6 m, jeleni se nepřekrývají a odstrkují se. Nemají se třást.

## 3. Útěk

- Poslat člověka ke stádu. Na 8 m se jeleni rozeběhnou přímo od něj rychlostí 0,6 m/tick, zastaví se na 16 m a v dalších idle krocích se vrátí ke kotvě. Sledovat, jestli návrat vypadá jako návrat, nebo jestli se stádo útěky postupně posouvá pryč.
- Pasoucí se jelen pastvu přeruší. Mládě odběhne od matky a po útěku se k ní vrátí (`FollowTask`).

## 4. Klik a karta

- Levý klik na jelena: karta s druhem, věkem a pohlavím, sytostí, „co dělá“ (grazing, Fleeing, Keeping close to its mother). Bez akcí. Hover obrys jako u lidí.
- Pravý klik bez vybraného člověka: nic. S vybraným člověkem: jedna položka **Hunt** (živý) nebo **Butcher** (mrtvý).
- Klik na jelena **nesmí vydat rozkaz** dříve vybranému člověku.

## 5. Lov a řezání (řízené)

- Vybrat člověka, pravý klik na jelena → Hunt. Člověk jde řízenou rychlostí (1,0 m/tick), na 10 m hází každé 3 ticky. Bez nástroje šance 5 %, se sekerou asi 35 %, s efektivní technikou ×1,5. Minutí jelena rozběhne. Karta člověka: „Hunting“. Zásah: jelen zemře (`Hunted`), mrtvola dostane maso 30, surovou kůži 1, kosti 4, šlachy 2.
- Pravý klik na mrtvého jelena → Butcher. Bez `efficient_butchering` bere jen maso a kosti; kůže a šlacha zůstanou na mrtvole. Karta mrtvoly vypisuje, co zbývá; „Nothing left“ až po vyčerpání.
- Maso je jídlo (5 hladu za jednotku, jablko 1): hladový člověk s masem v batohu ho sní sám. Zkusit i ruční Eat.

## 6. Autonomie

- Člověk, který lov a řezání zná, s prázdným batohem a hladem nad 25 vyrazí sám za mrtvolou s masem, nebo za stádem. Nasycený ne. Ověřit, že družina nezačne bezhlavě vybíjet stádo.
- Známé omezení: autonomní chůze je 0,3 m/tick, lov na 60 m vzdálené stádo trvá dlouho a někdo může po cestě vyhladovět. Milník říká „družina přetrvá“, ne „všichni přežijí“; rychlost chůze se ladit nebude, dokud není únava.

## 7. Zpracování a kažení

- Maso zmizí 30 ticků po řezání, ať leží kdekoli (batoh, sklad, hromádka, mrtvola). Položit a zvednout ho neomladí.
- Surová kůže vydrží 75 ticků. `rawhide_clothing` (2 surové kůže, bez další znalosti) hřeje stejně jako `warm_clothing`, ale po sezóně zmizí z batohu. Ve workshopu na surové kůži „Make“ (sloveso `tan`, skill `tanning`) → vyčiněná kůže → `warm_clothing` (2 kůže) napořád.
- Mrtvola jelena: maso zmizí po 30 ticích, tělo po 150 ticích zesvětlá do kostního tintu, kosti po dalším roce zmizí i s view. Mrtvý člověk zesvětlá stejně, ale nikdy nezmizí; pohřeb kostí dá vždy neoznačený hrob. Mrtvý jelen si **nelehne**, jen se zastaví a ztmavne. Známé omezení (art).

## 8. Rok a zima

- Nechat běžet celý rok. Očekávání ze shipped mapy: 17 → 17 → 22 → 18 živých po sezónách, telata na jaře (páření v `Mild`, gestace 150 ticků), v zimě umírají hlavně staří ze startovního stáda. Výrazně větší zimní úbytek = otevřená otázka okrsku 15 m vs. násobitele hladu 0,28 (viz `fauna-plan.md`).
- Lidé se po kroku 1b toulají kolem tábora (poloměr 8 m) a vracejí se k němu; nikdo sám neodejde dál než 60 m od tábora pro jídlo. Řízené rozkazy beze změny.

## 9. Výkon a log

- FPS s 17 tvory navíc oproti dřívějšku; kolize a hledání nejbližšího jsou O(n²).
- V logu hry (`%APPDATA%\Godot\app_userdata\ManyWinters Godot\logs\godot.log`) nesmí být `NullReferenceException` ani `at ManyWinters.`; E2E to hlídá, ale ruční hra dojde dál než testy.

## Co hlásit

Cokoli z bodů 2, 3, 5 a 8, co neodpovídá, plus dojem, jestli útěk na 8 m, zastavení na 16 m a návrat ke kotvě vypadají věrohodně. Podle výsledku se plánuje fáze 5 (`fauna-plan.md`).
