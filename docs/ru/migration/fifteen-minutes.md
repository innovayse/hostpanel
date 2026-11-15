---
title: Чек-лист миграции
description: Краткий чек-лист переноса из WHMCS в Hostpanel. Шаги, которые вы делаете вручную, занимают около пятнадцати минут, а сам импорт идёт в фоне.
keywords: "чек-лист миграции, импорт из whmcs, быстрая миграция, чек-лист импорта"
---

# Чек-лист миграции {#migration-checklist}

<Label type="admin" />

Это короткий путь переноса из WHMCS. Шаги, которые вы делаете вручную, занимают около пятнадцати минут. Затем импорт идёт в фоне, поэтому его длительность зависит от числа переносимых записей. Каждый пункт ведёт в раздел страницы [Переход с WHMCS](/ru/migration/from-another-system), где он описан.

- [ ] Прочитайте [Что понадобится](/ru/migration/from-another-system#what-you-need) и [Что не импортируется](/ru/migration/from-another-system#what-is-not-imported).
- [ ] Сделайте резервную копию базы данных и `.env`, настройте валюты, серверы и отделы, см. [Перед началом](/ru/migration/from-another-system#before-you-start).
- [ ] Создайте задание, установите дополнение и проверьте соединение, см. [Создание миграции](/ru/migration/from-another-system#create-a-migration).
- [ ] Запустите импорт и следите за счётчиками, см. [Статусы задания](/ru/migration/from-another-system#job-statuses).
- [ ] Разберите пропущенные записи и ошибки и повторите импорт, см. [Отчёт об импорте и причины пропуска](/ru/migration/import-report) и [Безопасный повторный запуск](/ru/migration/from-another-system#run-it-again-safely).
- [ ] Проверьте данные, см. [Проверка результата](/ru/migration/from-another-system#check-the-result).
- [ ] Удалите ключ, дополнение и завершённое задание, см. [После переезда](/ru/migration/from-another-system#after-the-move).
