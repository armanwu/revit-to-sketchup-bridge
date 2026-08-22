require 'sketchup.rb'
require 'json'

# Reads the JSON file produced by the Revit "Export to SketchUp" add-in and
# rebuilds the geometry inside the currently active SketchUp model:
#   - one Group per Revit element
#   - one Tag per "Category"
#   - material/color read per face
#   - "mesh" faces imported directly
#   - "cylinder" faces rebuilt as segmented quad strips with softened/smoothed
#     longitudinal edges
module RevitImporter

  COPLANAR_ANGLE_TOLERANCE_DEG = 1.0

  # Number of facets for a complete 360-degree cylindrical face.
  # Partial cylinders receive a proportional number of segments.
  CIRCLE_SEGMENTS = 24

  FULL_CIRCLE_TOLERANCE = 1e-4
  GEOMETRY_TOLERANCE = 1e-6

  # Default elements processed per UI timer tick
  BATCH_SIZE = 10

  def self.run
    path = UI.openpanel(
      'Select the Revit export JSON file',
      '',
      'JSON files|*.json||'
    )
    return unless path

    begin
      data = JSON.parse(File.read(path))
    rescue => e
      UI.messagebox("Failed to read the JSON file:\n#{e.class}: #{e.message}")
      return
    end

    elements = data['elements'] || []
    total_elements = elements.length

    if total_elements.zero?
      UI.messagebox('The selected JSON file contains no elements.')
      return
    end

    @cancelled = false

    # Create & show progress dialog if UI::HtmlDialog is available (SketchUp 2017+)
    dialog = create_progress_dialog if defined?(UI::HtmlDialog)
    dialog&.show

    model = Sketchup.active_model
    operation_started = false

    faces_created = 0
    faces_skipped = 0
    elements_created = 0
    elements_skipped = 0
    current_index = 0

    material_cache = {}

    material_for_color = lambda do |color|
      color = [200, 200, 200] unless color.is_a?(Array) && color.length >= 3

      r = [[color[0].to_i, 0].max, 255].min
      g = [[color[1].to_i, 0].max, 255].min
      b = [[color[2].to_i, 0].max, 255].min

      key = "#{r}_#{g}_#{b}"
      material_cache[key] ||= begin
        mat_name = "Revit_#{key}"
        mat = model.materials[mat_name] || model.materials.add(mat_name)
        mat.color = Sketchup::Color.new(r, g, b)
        mat
      end
    end

    begin
      model.start_operation('Import from Revit', true)
      operation_started = true
      root = model.active_entities

      batch_size = [(total_elements / 25.0).ceil, BATCH_SIZE].max

      timer_id = nil
      timer_id = UI.start_timer(0.01, true) do
        begin
          if @cancelled
            UI.stop_timer(timer_id) if timer_id
            timer_id = nil
            if operation_started
              model.abort_operation
              operation_started = false
            end
            dialog&.close
            Sketchup.status_text = 'Import from Revit cancelled.'
            UI.messagebox('Import cancelled by user.')
            next
          end

          end_index = [current_index + batch_size, total_elements].min

          (current_index...end_index).each do |element_index|
            el = elements[element_index]
            category = el['category'].to_s
            category = 'Uncategorized' if category.empty?

            layer_name = "Revit - #{category}"
            layer = model.layers[layer_name] || model.layers.add(layer_name)

            group = root.add_group
            group.name = "#{category} #{el['id']}".strip
            group.layer = layer

            element_faces_created = 0

            (el['faces'] || []).each_with_index do |face_data, face_index|
              unless group.valid?
                debug_error("Element #{element_index}, face #{face_index}: group became invalid")
                faces_skipped += 1
                next
              end

              begin
                entities = group.entities

                if face_data.is_a?(Hash)
                  face_type = face_data['type'] || 'mesh'
                  color = face_data['c']
                else
                  face_type = 'mesh'
                  color = nil
                end

                material = material_for_color.call(color)

                success = if face_type == 'cylinder'
                            add_cylindrical_face(entities, face_data, material)
                          else
                            verts = face_data.is_a?(Hash) ? (face_data['v'] || []) : face_data
                            add_mesh_face(entities, verts, material)
                          end

                if success
                  faces_created += 1
                  element_faces_created += 1
                else
                  faces_skipped += 1
                end
              rescue => face_error
                faces_skipped += 1
                debug_exception(face_error, "Element #{element_index}, face #{face_index}")
              end
            end

            if element_faces_created.zero?
              group.erase! if group.valid?
              elements_skipped += 1
              next
            end

            if group.valid?
              soften_coplanar_edges(group.entities)
              elements_created += 1
            else
              elements_skipped += 1
            end
          end

          current_index = end_index
          percent = total_elements.positive? ? ((current_index.to_f / total_elements.to_f) * 100).round : 100
          status_msg = "Imported #{current_index} of #{total_elements} elements..."

          update_progress(
            dialog,
            percent,
            status_msg,
            current_index,
            total_elements,
            elements_skipped,
            faces_created,
            faces_skipped
          )

          if current_index >= total_elements
            UI.stop_timer(timer_id) if timer_id
            timer_id = nil

            if operation_started
              model.commit_operation
              operation_started = false
            end

            close_dialog(dialog)
            Sketchup.status_text = 'Import from Revit complete.'

            UI.messagebox(
              "Import complete.\n\n" \
              "Elements created: #{elements_created}\n" \
              "Elements skipped: #{elements_skipped}\n" \
              "Faces created: #{faces_created}\n" \
              "Faces skipped: #{faces_skipped}\n" \
              "Unique materials used: #{material_cache.length}"
            )
          end
        rescue => batch_err
          UI.stop_timer(timer_id) if timer_id
          timer_id = nil
          if operation_started
            model.abort_operation
            operation_started = false
          end
          close_dialog(dialog)
          debug_exception(batch_err, 'Batch processing')
          UI.messagebox("Import failed during batch execution:\n#{batch_err.class}: #{batch_err.message}")
        end
      end
    rescue => top_err
      if operation_started
        model.abort_operation
        operation_started = false
      end
      close_dialog(dialog)

      debug_exception(top_err, 'Top-level import setup')
      UI.messagebox("Import failed to start:\n#{top_err.class}: #{top_err.message}")
    end
  end

  def self.create_progress_dialog
    dialog = UI::HtmlDialog.new({
      :dialog_title => 'Importing Revit Model',
      :preferences_key => 'RevitImporter_Progress',
      :scrollable => false,
      :resizable => false,
      :width => 440,
      :height => 250,
      :style => UI::HtmlDialog::STYLE_DIALOG
    })

    dialog.add_action_callback('cancel_import') do |_action_context|
      @cancelled = true
    end

    dialog.set_html(progress_html_content)
    dialog.center
    dialog
  end

  def self.close_dialog(dialog)
    dialog.close if dialog
  rescue => e
    debug_exception(e, 'close_dialog')
  end

  def self.update_progress(dialog, percent, status_text, current_el, total_el, el_skipped, face_created, face_skipped)
    status_bar_msg = "Importing Revit JSON: #{percent}% (#{current_el}/#{total_el})"
    Sketchup.status_text = status_bar_msg

    return unless dialog

    begin
      safe_status = escape_js(status_text)
      js = "updateProgress(#{percent}, '#{safe_status}', #{current_el}, #{total_el}, #{el_skipped}, #{face_created}, #{face_skipped});"
      dialog.execute_script(js)
    rescue => e
      debug_exception(e, 'update_progress execute_script')
    end
  end

  def self.escape_js(str)
    str.to_s.gsub('\\', '\\\\').gsub("'", "\\'").gsub("\n", ' ').gsub("\r", '')
  end

  def self.progress_html_content
    <<~HTML
      <!DOCTYPE html>
      <html>
      <head>
      <meta charset="utf-8">
      <style>
        body {
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
          background-color: #1e1e24;
          color: #f0f0f5;
          margin: 0;
          padding: 16px;
          user-select: none;
          box-sizing: border-box;
        }
        .title {
          font-size: 15px;
          font-weight: 600;
          margin-bottom: 12px;
          color: #ffffff;
          display: flex;
          align-items: center;
          gap: 8px;
        }
        .progress-container {
          width: 100%;
          background-color: #2a2a35;
          border-radius: 8px;
          height: 20px;
          overflow: hidden;
          margin-bottom: 12px;
          box-shadow: inset 0 1px 3px rgba(0,0,0,0.3);
        }
        .progress-bar {
          height: 100%;
          width: 0%;
          background: linear-gradient(90deg, #007acc, #00b4d8);
          transition: width 0.15s ease-out;
          border-radius: 8px;
        }
        .status-row {
          display: flex;
          justify-content: space-between;
          font-size: 13px;
          margin-bottom: 10px;
          color: #cbd5e1;
        }
        .percent-text {
          font-weight: 700;
          color: #38bdf8;
        }
        .stats-grid {
          display: grid;
          grid-template-columns: 1fr 1fr;
          gap: 8px;
          background: #141419;
          padding: 10px 12px;
          border-radius: 6px;
          font-size: 12px;
          color: #94a3b8;
          margin-bottom: 14px;
          border: 1px solid #2e2e3a;
        }
        .stats-item span {
          color: #f1f5f9;
          font-weight: 600;
        }
        .btn-container {
          display: flex;
          justify-content: flex-end;
        }
        button {
          background-color: #334155;
          color: #f8fafc;
          border: none;
          padding: 6px 16px;
          border-radius: 5px;
          font-size: 12px;
          font-weight: 600;
          cursor: pointer;
          transition: background-color 0.2s;
        }
        button:hover {
          background-color: #ef4444;
          color: #ffffff;
        }
      </style>
      <script>
        function cancelImport() {
          var btn = document.getElementById('cancel-btn');
          if (btn) {
            btn.innerText = 'Cancelling...';
            btn.disabled = true;
          }
          if (window.sketchup && sketchup.cancel_import) {
            sketchup.cancel_import();
          }
        }
        function updateProgress(percent, statusText, elCurrent, elTotal, elSkipped, faceCreated, faceSkipped) {
          var pbar = document.getElementById('pbar');
          var ptext = document.getElementById('percent-text');
          var stext = document.getElementById('status-text');
          var ecount = document.getElementById('el-count');
          var eskipped = document.getElementById('el-skipped');
          var fcreated = document.getElementById('face-created');
          var fskipped = document.getElementById('face-skipped');

          if (pbar) pbar.style.width = percent + '%';
          if (ptext) ptext.innerText = percent + '%';
          if (stext) stext.innerText = statusText;
          if (ecount) ecount.innerText = elCurrent + ' / ' + elTotal;
          if (eskipped) eskipped.innerText = elSkipped;
          if (fcreated) fcreated.innerText = faceCreated;
          if (fskipped) fskipped.innerText = faceSkipped;
        }
      </script>
      </head>
      <body>
        <div class="title">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#38bdf8" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
          Importing Revit Model
        </div>
        <div class="progress-container">
          <div id="pbar" class="progress-bar"></div>
        </div>
        <div class="status-row">
          <span id="status-text">Preparing import...</span>
          <span id="percent-text" class="percent-text">0%</span>
        </div>
        <div class="stats-grid">
          <div class="stats-item">Elements: <span id="el-count">0 / 0</span></div>
          <div class="stats-item">Skipped Elements: <span id="el-skipped">0</span></div>
          <div class="stats-item">Faces Created: <span id="face-created">0</span></div>
          <div class="stats-item">Faces Skipped: <span id="face-skipped">0</span></div>
        </div>
        <div class="btn-container">
          <button id="cancel-btn" onclick="cancelImport()">Cancel</button>
        </div>
      </body>
      </html>
    HTML
  end

  # Adds one triangulated ("mesh") face from [x,y,z] points.
  def self.add_mesh_face(entities, verts, material)
    return false unless verts.is_a?(Array) && verts.length >= 3

    pts = verts.map do |v|
      return false unless v.is_a?(Array) && v.length >= 3
      Geom::Point3d.new(v[0].to_f, v[1].to_f, v[2].to_f)
    end

    face = entities.add_face(pts)
    return false unless face && face.valid?

    face.material = material
    face.back_material = material
    true
  rescue => e
    debug_exception(e, 'add_mesh_face')
    false
  end

  # Rebuilds a cylindrical face as a strip of planar quads, then marks the
  # shared longitudinal edges soft + smooth so it renders as one curved wall.
  def self.add_cylindrical_face(entities, face_data, material)
    return false unless face_data.is_a?(Hash)

    origin_a = face_data['origin']
    axis_a = face_data['axis']
    ref_a = face_data['refDir']
    radius = face_data['radius'].to_f
    angle_span = face_data['angleSpan'].to_f
    height = face_data['height'].to_f

    return false unless vector_array?(origin_a) && vector_array?(axis_a) && vector_array?(ref_a)
    return false if radius <= GEOMETRY_TOLERANCE
    return false if height.abs <= GEOMETRY_TOLERANCE
    return false if angle_span <= GEOMETRY_TOLERANCE
    return false if angle_span > (2.0 * Math::PI + FULL_CIRCLE_TOLERANCE)

    bottom_center = Geom::Point3d.new(
      origin_a[0].to_f,
      origin_a[1].to_f,
      origin_a[2].to_f
    )

    axis_vec = Geom::Vector3d.new(
      axis_a[0].to_f,
      axis_a[1].to_f,
      axis_a[2].to_f
    )

    ref_dir = Geom::Vector3d.new(
      ref_a[0].to_f,
      ref_a[1].to_f,
      ref_a[2].to_f
    )

    return false unless axis_vec.valid? && ref_dir.valid?
    return false if axis_vec.length <= GEOMETRY_TOLERANCE
    return false if ref_dir.length <= GEOMETRY_TOLERANCE

    axis_vec.normalize!

    # Re-orthogonalize ref_dir against axis_vec (guards against FP drift).
    ref_dir = Geom::Vector3d.linear_combination(
      1.0, ref_dir,
      -ref_dir.dot(axis_vec), axis_vec
    )
    return false unless ref_dir.valid? && ref_dir.length > GEOMETRY_TOLERANCE
    ref_dir.normalize!

    full_circle = angle_span >= (2.0 * Math::PI - FULL_CIRCLE_TOLERANCE)
    effective_span = full_circle ? (2.0 * Math::PI) : angle_span

    segments = ((CIRCLE_SEGMENTS * effective_span) / (2.0 * Math::PI)).ceil
    segments = 2 if segments < 2
    segments = CIRCLE_SEGMENTS if segments > CIRCLE_SEGMENTS

    start_point = bottom_center.offset(ref_dir, radius)

    bottom_points = []
    0.upto(segments) do |i|
      if full_circle && i == segments
        # Reuse the exact same point so the seam merges reliably.
        bottom_points << bottom_points[0]
      else
        angle = effective_span * i.to_f / segments.to_f
        rotation = Geom::Transformation.rotation(bottom_center, axis_vec, angle)
        bottom_points << start_point.transform(rotation)
      end
    end

    top_points = bottom_points.map do |point|
      point.offset(axis_vec, height)
    end

    created_faces = []
    shared_edge_counts = Hash.new(0)
    shared_edges = {}

    segments.times do |i|
      pts = [
        bottom_points[i],
        bottom_points[i + 1],
        top_points[i + 1],
        top_points[i]
      ]

      face = entities.add_face(pts)
      next unless face && face.valid?

      face.material = material
      face.back_material = material
      created_faces << face

      # Edges shared by two segments are the longitudinal seams; soften those.
      face.edges.each do |edge|
        next unless edge.valid?
        id = edge.entityID
        shared_edges[id] = edge
        shared_edge_counts[id] += 1
      end
    end

    return false if created_faces.empty?

    shared_edge_counts.each do |id, count|
      next unless count >= 2
      edge = shared_edges[id]
      next unless edge && edge.valid?

      begin
        edge.soft = true
        edge.smooth = true
      rescue => e
        debug_exception(e, 'soften cylinder seam')
      end
    end

    true
  rescue => e
    debug_exception(e, 'add_cylindrical_face')
    false
  end

  # Hides triangulation diagonals only when the neighboring faces are nearly
  # coplanar. Cylinder seams are handled directly in add_cylindrical_face.
  def self.soften_coplanar_edges(entities)
    tolerance = COPLANAR_ANGLE_TOLERANCE_DEG.degrees

    entities.grep(Sketchup::Edge).each do |edge|
      next unless edge.valid?

      begin
        faces = edge.faces.select(&:valid?)
        next unless faces.length == 2

        angle = faces[0].normal.angle_between(faces[1].normal)
        next unless angle <= tolerance

        edge.soft = true
        edge.smooth = true
      rescue => e
        debug_exception(e, 'soften_coplanar_edges')
      end
    end
  rescue => e
    debug_exception(e, 'soften_coplanar_edges collection')
  end

  def self.vector_array?(value)
    value.is_a?(Array) && value.length >= 3
  end

  def self.debug_error(message)
    puts("[RevitImporter] #{message}")
  rescue
    nil
  end

  def self.debug_exception(error, context = nil)
    prefix = context ? "[RevitImporter] #{context}" : '[RevitImporter]'
    puts("#{prefix}: #{error.class}: #{error.message}")
    (error.backtrace || [])[0, 10].each { |line| puts("  #{line}") }
  rescue
    nil
  end

  unless file_loaded?(__FILE__)
    UI.menu('Extensions').add_item('Import from Revit (JSON)') do
      RevitImporter.run
    end
    file_loaded(__FILE__)
  end

end
