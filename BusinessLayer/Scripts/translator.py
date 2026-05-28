import sys
import json
from googletrans import Translator

# Tərcümə edəcək əsas funksiya
def translate(text, target_langs):
    translator = Translator()
    translations = {}
    
    # Hər dil üçün tərcüməni icra edirik
    for lang in target_langs:
        try:
            # src='az' mənbə dili kimi Azərbaycancanı təyin edir
            result = translator.translate(text, src='az', dest=lang)
            translations[lang] = result.text
        except Exception as e:
            # Xəta olarsa (məsələn, API bloklansa), boş dəyər qaytarır
            translations[lang] = ""
            
    return translations

# Skript C#-dan çağırıldıqda işə düşür
if __name__ == "__main__":
    # C#-dan gələn mətn və dillər arqument kimi qəbul edilir
    if len(sys.argv) > 2:
        text_to_translate = sys.argv[1]
        target_langs_str = sys.argv[2]
        
        # Target dillər siyahısını bərpa edirik
        target_langs = target_langs_str.split(',')
        
        result_dict = translate(text_to_translate, target_langs)
        
        # Nəticəni JSON formatında çap edirik (C# bunu oxuyacaq)
        print(json.dumps(result_dict))

    else:
        # Arqument yoxdursa boş JSON qaytar
        print(json.dumps({}))