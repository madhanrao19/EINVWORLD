// Full Content editor for Admin Resources Create/Edit (TinyMCE 7, self-hosted).
// Expects <textarea id="Resource_ContentHtml" data-upload-url="...?handler=UploadImage" data-content-css="...">.
// Pasted Word/Docs/web content keeps its structure (headings, lists, bold, links, tables) but drops fonts,
// sizes, colours and backgrounds so the article follows the site stylesheet on every screen. The server
// sanitises again on save (ResourceHtmlSanitizer) — this is only the friendly first pass.
document.addEventListener("DOMContentLoaded", function () {
    var textarea = document.getElementById("Resource_ContentHtml");
    if (!textarea || typeof tinymce === "undefined") return;

    var uploadUrl = textarea.dataset.uploadUrl;
    var dark = document.documentElement.getAttribute("data-bs-theme") === "dark";

    function cleanPastedNode(root) {
        root.querySelectorAll("[style]").forEach(function (el) {
            var align = el.style.textAlign;
            el.removeAttribute("style");
            if (align && align !== "left" && align !== "start" && align !== "justify") el.style.textAlign = align;
        });
        var junk = ["class", "lang", "align", "bgcolor", "color", "face", "size", "id"];
        root.querySelectorAll("*").forEach(function (el) {
            junk.forEach(function (a) { el.removeAttribute(a); });
        });
        // Unwrap <font> and attribute-less <span> left behind by Word/Docs.
        root.querySelectorAll("font, span").forEach(function (el) {
            if (el.tagName === "FONT" || !el.attributes.length) el.replaceWith.apply(el, Array.from(el.childNodes));
        });
        // Word separates blocks with empty paragraphs; spacing comes from the stylesheet instead.
        root.querySelectorAll("p").forEach(function (p) {
            if (!p.textContent.trim() && !p.querySelector("img")) p.remove();
        });
    }

    tinymce.init({
        target: textarea,
        skin: dark ? "oxide-dark" : "oxide",
        // The article stylesheet, not the app theme: the editor shows what visitors see on the public page.
        content_css: textarea.dataset.contentCss,
        body_class: "article-body",
        height: 500,
        menubar: false,
        plugins: "image link code lists advlist autoresize autolink table",
        toolbar: "undo redo | blocks | bold italic underline | alignleft aligncenter alignright | bullist numlist blockquote | link unlink | image table | removeformat code",
        block_formats: "Paragraph=p; Heading 2=h2; Heading 3=h3; Heading 4=h4; Heading 1=h1",
        branding: false,
        resize: true,
        min_height: 400,
        autoresize_bottom_margin: 30,

        // Paste: keep structure, drop presentation.
        paste_data_images: true,
        paste_webkit_styles: "text-align",
        paste_remove_styles_if_webkit: true,
        paste_merge_formats: true,
        paste_postprocess: function (editor, args) { cleanPastedNode(args.node); },
        valid_styles: { "*": "text-align" },
        invalid_elements: "font,center,o:p,style,script",
        table_default_attributes: {},
        table_default_styles: {},

        link_title: false,
        link_assume_external_targets: true,
        link_context_toolbar: true,
        link_default_protocol: "https",
        target_list: [
            { title: "Same window", value: "" },
            { title: "New window", value: "_blank" }
        ],
        link_list: [
            { title: "Home Page", value: "/" },
            { title: "About Us", value: "/about" },
            { title: "Contact Us", value: "/contact" },
            { title: "Resources", value: "/resources" }
        ],

        automatic_uploads: true,
        file_picker_types: "image",
        images_upload_url: uploadUrl,
        // TinyMCE 7 expects a Promise resolving to the image URL.
        images_upload_handler: function (blobInfo) {
            var formData = new FormData();
            formData.append("image", blobInfo.blob(), blobInfo.filename());
            var token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (token) formData.append("__RequestVerificationToken", token.value);
            return fetch(uploadUrl, { method: "POST", body: formData })
                .then(function (res) {
                    if (!res.ok) throw new Error("Upload failed (" + res.status + ")");
                    return res.json();
                })
                .then(function (data) {
                    if (!data.location) throw new Error("Server did not return the image location.");
                    return data.location;
                });
        }
    }).catch(function (error) {
        console.error("TinyMCE initialisation failed:", error);
    });

    var form = textarea.closest("form");
    if (form) form.addEventListener("submit", function () { tinymce.triggerSave(); });
});
